using OpenVoiceLab;
using Xunit;

namespace OpenVoiceLab.Tests;

public class WorkerSupervisorTests
{
    [Fact]
    public void BuildHealthUrlUsesPort()
    {
        var supervisor = new WorkerSupervisor();
        var port = supervisor.FindFreePort();
        var url = $"http://127.0.0.1:{port}/health";
        Assert.Contains($":{port}/health", url);
    }
}
