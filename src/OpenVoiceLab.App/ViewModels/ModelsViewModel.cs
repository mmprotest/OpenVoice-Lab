using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenVoiceLab.Shared;
using Windows.System;
using Microsoft.UI.Dispatching;

namespace OpenVoiceLab.ViewModels;

public partial class ModelsViewModel : ObservableObject
{
    private static readonly (string Kind, string Size)[] RequiredModels =
    {
        ("custom_voice", "0.6b"),
        ("base", "0.6b"),
        ("voice_design", "1.7b")
    };

    private readonly AppServices _services;
    private readonly DispatcherQueue _dispatcher;
    private readonly Dictionary<string, ModelItemViewModel> _modelLookup = new();

    public ObservableCollection<ModelItemViewModel> Models { get; } = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    public ModelsViewModel(AppServices services)
    {
        _services = services;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }
        IsBusy = true;
        try
        {
            var api = await _services.GetApiAsync();
            var system = await api.GetSystemAsync();
            var status = await api.GetModelsStatusAsync();
            _services.ModelsStatus.UpdateStatuses(status.Models);

            Models.Clear();
            _modelLookup.Clear();
            foreach (var model in system.ModelsSupported.OrderBy(model => model.Kind).ThenBy(model => model.Size))
            {
                var vm = new ModelItemViewModel(model);
                var modelStatus = status.Models.FirstOrDefault(entry => entry.ModelId == model.ModelId);
                if (modelStatus != null)
                {
                    vm.UpdateFromStatus(modelStatus);
                }
                Models.Add(vm);
                _modelLookup[model.ModelId] = vm;
            }
            StatusMessage = "Model status refreshed";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load models: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshAsync()
    {
        var api = await _services.GetApiAsync();
        var status = await api.GetModelsStatusAsync();
        _services.ModelsStatus.UpdateStatuses(status.Models);
        foreach (var entry in status.Models)
        {
            if (_modelLookup.TryGetValue(entry.ModelId, out var model))
            {
                model.UpdateFromStatus(entry);
            }
        }
    }

    public async Task DownloadAsync(ModelItemViewModel model)
    {
        if (model.IsDownloaded || model.IsDownloading)
        {
            return;
        }
        var api = await _services.GetApiAsync();
        model.Error = null;
        model.IsDownloading = true;
        model.Status = "downloading";

        var downloadTask = api.StartModelDownloadAsync(model.ModelId);
        try
        {
            await StreamEventsWithRetryAsync(api, model);
            await downloadTask;
        }
        catch (Exception ex)
        {
            model.Error = ex.Message;
            StatusMessage = $"Download failed for {model.DisplayName}: {ex.Message}";
        }
        finally
        {
            await RefreshAsync();
        }
    }

    public async Task DownloadRequiredAsync()
    {
        foreach (var model in Models.Where(IsRequired).ToList())
        {
            if (!model.IsDownloaded)
            {
                await DownloadAsync(model);
            }
        }
    }

    public async Task OpenModelsFolderAsync(ModelItemViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Path))
        {
            return;
        }
        await Launcher.LaunchFolderPathAsync(model.Path);
    }

    private static bool IsRequired(ModelItemViewModel model)
    {
        return RequiredModels.Any(required =>
            model.Kind.Equals(required.Kind, StringComparison.OrdinalIgnoreCase)
            && model.Size.Equals(required.Size, StringComparison.OrdinalIgnoreCase));
    }

    private async Task StreamEventsWithRetryAsync(ApiClient api, ModelItemViewModel model)
    {
        var attempts = 0;
        while (attempts < 3)
        {
            try
            {
                await foreach (var evt in api.StreamModelDownloadEventsAsync(model.ModelId))
                {
                    _dispatcher.TryEnqueue(() =>
                    {
                        model.UpdateFromEvent(evt);
                        if (!string.IsNullOrWhiteSpace(evt.Error))
                        {
                            StatusMessage = evt.Error;
                        }
                    });
                }
                return;
            }
            catch (Exception ex)
            {
                attempts++;
                if (attempts >= 3)
                {
                    throw new InvalidOperationException("SSE stream failed.", ex);
                }
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }
}
