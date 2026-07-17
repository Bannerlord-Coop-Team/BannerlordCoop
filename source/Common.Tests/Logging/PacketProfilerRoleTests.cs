using Common.Logging;
using System;
using Xunit;

namespace Common.Tests.Logging;

/// <summary>
/// Verifies packet profiling stays disabled on clients.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public sealed class PacketProfilerRoleTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;

    public void Dispose()
    {
        ModInformation.IsServer = wasServer;
    }

    [Fact]
    public void Record_WhenPacketIsNull_ReturnsOnClientAndThrowsOnServer()
    {
        using var profiler = new PacketProfiler(TimeSpan.FromDays(1));

        ModInformation.IsServer = false;
        profiler.Record(null!, 1);

        ModInformation.IsServer = true;
        Assert.Throws<ArgumentNullException>(() => profiler.Record(null!, 1));
    }
}
