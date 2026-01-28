using OpenVoiceLab;
using Xunit;

namespace OpenVoiceLab.Tests;

public class WorkerSupervisorTests
{
    [Fact]
    public void BuildHealthUrlUsesPort()
    {
        var supervisor = new WorkerSupervisor();
        var port = supervisor.FindFreePort(20000, 40000);
        Assert.InRange(port, 1, 65535);
        Assert.True(port >= 20000);
    }
}
