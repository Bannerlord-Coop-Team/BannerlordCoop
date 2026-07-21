using Coop.Core.Server.Services.Instances;
using LiteNetLib;
using System.Net;
using System.Reflection;
using Xunit;

namespace Coop.Tests.Server.Services.Instances;

public class MissionManagerTests
{
    private static readonly ConstructorInfo PeerConstructor = typeof(NetPeer).GetConstructor(
        BindingFlags.NonPublic | BindingFlags.Instance,
        binder: null,
        new[] { typeof(NetManager), typeof(IPEndPoint), typeof(int) },
        modifiers: null)!;

    [Fact]
    public void EntryReportsFirstMemberFromTheAtomicMembershipUpdate()
    {
        var manager = new MissionManager();
        var first = CreatePeer(1);
        var second = CreatePeer(2);

        Assert.True(manager.TryEnterMission(first, "first", "battle", out var firstExisting, out var isFirst));
        Assert.True(isFirst);
        Assert.Empty(firstExisting);

        Assert.True(manager.TryEnterMission(second, "second", "battle", out var secondExisting, out isFirst));
        Assert.False(isFirst);
        Assert.Single(secondExisting);
    }

    [Fact]
    public void EmptyConclusionClaimRejectsReentrantEntry()
    {
        var manager = new MissionManager();
        bool entered = true;

        Assert.True(manager.TryClaimEmptyInstance("battle", () =>
            entered = manager.TryEnterMission(
                CreatePeer(1), "late", "battle", out _, out _)));

        Assert.False(entered);
    }

    private static NetPeer CreatePeer(int id)
        => (NetPeer)PeerConstructor.Invoke(new object[]
        {
            new NetManager(null),
            new IPEndPoint(IPAddress.Loopback, 52000 + id),
            id,
        });
}
