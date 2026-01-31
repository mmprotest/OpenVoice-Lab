using OpenVoiceLab;
using Xunit;

namespace OpenVoiceLab.Tests;

public class WorkerSupervisorTests
{
    [Fact]
    public void ParsesWorkerPortLine()
    {
        var parsed = WorkerSupervisor.TryParseWorkerPort("WORKER_PORT=25001", out var port);
        Assert.True(parsed);
        Assert.Equal(25001, port);
    }
}
