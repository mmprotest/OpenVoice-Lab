using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenVoiceLab.Shared;

namespace OpenVoiceLab.ViewModels;

public partial class ProjectsViewModel : ObservableObject
{
    private readonly AppServices _services;

    public ObservableCollection<ProjectInfo> Projects { get; } = new();

    [ObservableProperty]
    private ProjectInfo? _selectedProject;

    [ObservableProperty]
    private string? _status;

    public ProjectsViewModel(AppServices services)
    {
        _services = services;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var api = await _services.GetApiAsync();
        var response = await api.GetProjectsAsync();
        Projects.Clear();
        foreach (var project in response.Projects)
        {
            Projects.Add(project);
        }
        SelectedProject = Projects.FirstOrDefault(p => p.ProjectId == SettingsStore.CurrentProjectId) ?? Projects.FirstOrDefault();
        Status = "Loaded projects";
    }

    public async Task CreateAsync(string name)
    {
        var api = await _services.GetApiAsync();
        var response = await api.CreateProjectAsync(new ProjectCreateRequest(name));
        SettingsStore.CurrentProjectId = response.ProjectId;
        Status = "Project created";
        await LoadAsync();
    }

    public void SelectProject(ProjectInfo project)
    {
        SettingsStore.CurrentProjectId = project.ProjectId;
        SelectedProject = project;
        Status = $"Selected {project.Name}";
    }
}
