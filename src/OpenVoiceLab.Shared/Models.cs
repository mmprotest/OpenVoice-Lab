namespace OpenVoiceLab.Shared;

public record HealthResponse(bool Ok, string Version);

public record SystemInfo(
    bool CudaAvailable,
    IReadOnlyList<GpuInfo> Gpus,
    IReadOnlyList<string> Backends,
    IReadOnlyList<ModelSupport> ModelsSupported,
    int DefaultSampleRate
);

public record GpuInfo(string Name);

public record ModelSupport(string ModelId, string Size);

public record VoiceInfo(string VoiceId, string Name, string Type, IReadOnlyList<string>? Tags = null);

public record VoicesResponse(IReadOnlyList<VoiceInfo> Voices);

public record TtsRequest(
    string VoiceId,
    string Text,
    string Language,
    string? Style,
    string ModelSize,
    string Backend,
    int SampleRate,
    bool EnableSsmlLite,
    string? PronunciationProfileId,
    string? ProjectId
);

public record TtsResponse(
    string JobId,
    string OutputPath,
    int DurationMs,
    string BackendUsed,
    string? Warning
);
