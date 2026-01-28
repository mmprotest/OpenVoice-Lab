using System.Text.Json;
using OpenVoiceLab.Shared;
using Xunit;

namespace OpenVoiceLab.Tests;

public class ApiClientTests
{
    [Fact]
    public void HealthResponseDeserializes()
    {
        var json = "{\"ok\":true,\"version\":\"1.0.0\"}";
        var result = JsonSerializer.Deserialize<HealthResponse>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(result);
        Assert.True(result!.Ok);
        Assert.Equal("1.0.0", result.Version);
    }

    [Fact]
    public void ModelsStatusDeserializes()
    {
        var json = "{\"models\":[{\"modelId\":\"Qwen/Qwen3-TTS-12Hz-0.6B-CustomVoice\",\"kind\":\"custom_voice\",\"size\":\"0.6b\",\"status\":\"completed\",\"downloadedBytes\":1024,\"totalBytes\":2048,\"progress\":50,\"path\":\"C:/models\",\"error\":null}]}";
        var result = JsonSerializer.Deserialize<ModelsStatusResponse>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(result);
        Assert.Single(result!.Models);
        Assert.Equal("custom_voice", result.Models[0].Kind);
    }
}
