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

public record ModelSupport(string ModelId, string Kind, string Size);

public record ModelStatus(
    string ModelId,
    string Kind,
    string Size,
    string Status,
    long DownloadedBytes,
    long TotalBytes,
    int Progress,
    string Path,
    string? Error
);

public record ModelsStatusResponse(IReadOnlyList<ModelStatus> Models);

public record ModelDownloadRequest(string ModelId);

public record ModelDownloadResponse(bool Ok, string Path);

public record ModelDownloadEvent(
    int Pct,
    string Stage,
    long DownloadedBytes,
    long TotalBytes,
    string? Error
);

public record VoiceInfo(
    string VoiceId,
    string Name,
    string Type,
    IReadOnlyList<string>? Tags = null,
    string? CreatedAt = null,
    string? Description = null,
    string? RefText = null
);

public record VoicesResponse(IReadOnlyList<VoiceInfo> Voices);

public record VoiceCloneResponse(string VoiceId);

public record VoiceDesignResponse(string VoiceId);

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

public record ProjectInfo(string ProjectId, string Name, string CreatedAt);

public record ProjectsResponse(IReadOnlyList<ProjectInfo> Projects);

public record ProjectCreateRequest(string Name);

public record ProjectCreateResponse(string ProjectId);

public record HistoryEntry(
    string JobId,
    string Text,
    string VoiceId,
    string OutputPath,
    string CreatedAt,
    string? ProjectId
);

public record HistoryResponse(IReadOnlyList<HistoryEntry> History);

public record PronunciationEntry(string From, string To);

public record PronunciationProfile(string ProfileId, string Name, string CreatedAt, IReadOnlyList<PronunciationEntry> Entries);

public record PronunciationProfilesResponse(IReadOnlyList<PronunciationProfile> Profiles);

public record PronunciationProfileCreateRequest(string Name);

public record PronunciationProfileCreateResponse(string Id);

public record PronunciationProfileUpdateRequest(IReadOnlyList<PronunciationEntry> Entries);

public record DeleteDataResponse(bool Ok);
