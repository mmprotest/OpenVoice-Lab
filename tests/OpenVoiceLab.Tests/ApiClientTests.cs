using System.Text.Json;
using OpenVoiceLab.Shared;
using Xunit;

namespace OpenVoiceLab.Tests;

public class ApiClientTests
{
    [Fact]
    public void HealthResponseDeserializes()
    {
        var json = "{\"ok\":true,\"version\":\"0.1.0\"}";
        var result = JsonSerializer.Deserialize<HealthResponse>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Assert.NotNull(result);
        Assert.True(result!.Ok);
        Assert.Equal("0.1.0", result.Version);
    }
}
