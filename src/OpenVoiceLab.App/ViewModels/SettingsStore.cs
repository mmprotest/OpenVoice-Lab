using Windows.Storage;

namespace OpenVoiceLab.ViewModels;

public static class SettingsStore
{
    private const string DefaultBackendKey = "DefaultBackend";
    private const string DefaultModelSizeKey = "DefaultModelSize";
    private const string KeepRefAudioKey = "KeepRefAudio";
    private const string CurrentProjectKey = "CurrentProjectId";

    private static ApplicationDataContainer Local => ApplicationData.Current.LocalSettings;

    public static string DefaultBackend
    {
        get => (string?)Local.Values[DefaultBackendKey] ?? "auto";
        set => Local.Values[DefaultBackendKey] = value;
    }

    public static string DefaultModelSize
    {
        get => (string?)Local.Values[DefaultModelSizeKey] ?? "0.6b";
        set => Local.Values[DefaultModelSizeKey] = value;
    }

    public static bool KeepRefAudio
    {
        get => (bool?)Local.Values[KeepRefAudioKey] ?? false;
        set => Local.Values[KeepRefAudioKey] = value;
    }

    public static string? CurrentProjectId
    {
        get
        {
            var value = (string?)Local.Values[CurrentProjectKey];
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        set => Local.Values[CurrentProjectKey] = value ?? string.Empty;
    }
}
