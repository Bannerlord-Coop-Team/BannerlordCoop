using Coop.Core.Server.Services.Instances;
using LiteNetLib;
using System;
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

        Assert.True(manager.TryBeginEmptyInstanceConclusion("battle"));
        Assert.False(manager.TryEnterMission(CreatePeer(1), "late", "battle", out _, out _));
        manager.CompleteInstanceConclusion("battle", succeeded: true);
    }

    [Fact]
    public void NatOnlyShellDoesNotBlockEmptyConclusionClaim()
    {
        var manager = new MissionManager();
        var netManager = new NetManager(null);
        var local = new IPEndPoint(IPAddress.Loopback, 53001);
        var remote = new IPEndPoint(IPAddress.Loopback, 53002);

        manager.HandleIntroductionRequest(netManager.NatPunchModule, local, remote, "late%battle");

        Assert.False(manager.TryGetControllers("battle", out _));
        Assert.True(manager.TryBeginEmptyInstanceConclusion("battle"));
        manager.CompleteInstanceConclusion("battle", succeeded: true);
        Assert.False(manager.TryEnterMission(CreatePeer(1), "late", "battle", out _, out _));
    }

    [Fact]
    public void FailedConclusionRestoresNatOnlyShell()
    {
        var manager = new MissionManager();
        var netManager = new NetManager(null);
        var local = new IPEndPoint(IPAddress.Loopback, 53003);
        var remote = new IPEndPoint(IPAddress.Loopback, 53004);

        manager.HandleIntroductionRequest(netManager.NatPunchModule, local, remote, "late%battle");

        Assert.True(manager.TryBeginEmptyInstanceConclusion("battle"));
        manager.CompleteInstanceConclusion("battle", succeeded: false);
        Assert.True(manager.TryEnterMission(CreatePeer(1), "late", "battle", out _, out _));
    }

    [Fact]
    public void ActiveConclusionClaimFencesLaterEntry()
    {
        var manager = new MissionManager();

        Assert.True(manager.TryEnterMission(CreatePeer(1), "host", "battle", out _, out _));
        Assert.True(manager.TryBeginActiveInstanceConclusion("battle", new[] { "host" }));
        manager.CompleteInstanceConclusion("battle", succeeded: true);
        Assert.False(manager.TryEnterMission(CreatePeer(2), "late", "battle", out _, out _));
    }

    [Fact]
    public void FailedActiveConclusionReopensEntryAndRetry()
    {
        var manager = new MissionManager();

        Assert.True(manager.TryEnterMission(CreatePeer(1), "host", "battle", out _, out _));
        Assert.True(manager.TryBeginActiveInstanceConclusion("battle", new[] { "host" }));
        manager.CompleteInstanceConclusion("battle", succeeded: false);

        Assert.True(manager.TryEnterMission(CreatePeer(2), "late", "battle", out _, out _));
        Assert.True(manager.TryBeginActiveInstanceConclusion("battle", new[] { "host", "late" }));
    }

    [Fact]
    public void ActiveConclusionClaimRejectsChangedMembership()
    {
        var manager = new MissionManager();

        Assert.True(manager.TryEnterMission(CreatePeer(1), "host", "battle", out _, out _));
        Assert.True(manager.TryEnterMission(CreatePeer(2), "late", "battle", out _, out _));

        Assert.False(manager.TryBeginActiveInstanceConclusion("battle", new[] { "host" }));
    }

    private static NetPeer CreatePeer(int id)
        => (NetPeer)PeerConstructor.Invoke(new object[]
        {
            new NetManager(null),
            new IPEndPoint(IPAddress.Loopback, 52000 + id),
            id,
        });
}
