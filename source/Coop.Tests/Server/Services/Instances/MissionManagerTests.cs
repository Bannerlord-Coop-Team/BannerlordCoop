using Coop.Core.Server.Services.Instances;
using LiteNetLib;
using System;
using System.Collections.Generic;
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

        Assert.True(manager.TryEnterMission(first, "first", "battle", out var firstEntry));
        Assert.True(firstEntry.IsFirstMember);
        Assert.Empty(firstEntry.ExistingMembers);

        Assert.True(manager.TryEnterMission(second, "second", "battle", out var secondEntry));
        Assert.False(secondEntry.IsFirstMember);
        Assert.Single(secondEntry.ExistingMembers);
    }

    [Fact]
    public void EmptyConclusionClaimRejectsReentrantEntry()
    {
        var manager = new MissionManager();

        Assert.True(manager.TryBeginEmptyInstanceConclusion("battle"));
        Assert.False(manager.TryEnterMission(CreatePeer(1), "late", "battle", out _));
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
        Assert.False(manager.TryEnterMission(CreatePeer(1), "late", "battle", out _));
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
        Assert.True(manager.TryEnterMission(CreatePeer(1), "late", "battle", out _));
    }

    [Fact]
    public void GracefulLeaveRemovesControllerPunchEndpoint()
    {
        var manager = new MissionManager();
        var departingPeer = CreatePeer(1);
        var survivorPeer = CreatePeer(2);
        var netManager = new NetManager(null);
        var internalEndpoint = new IPEndPoint(IPAddress.Loopback, 53005);
        var externalEndpoint = new IPEndPoint(IPAddress.Loopback, 53006);

        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            internalEndpoint,
            externalEndpoint,
            "departing%battle");
        Assert.True(manager.TryEnterMission(departingPeer, "departing", "battle", out _));
        Assert.True(manager.TryEnterMission(survivorPeer, "survivor", "battle", out _));

        Assert.True(manager.TryLeaveMission(departingPeer, "departing", "battle", out _));

        MissionInstance instance = GetInstance(manager, "battle");
        Assert.Empty(instance.PunchEndpoints);
        Assert.Equal(new[] { "survivor" }, instance.Controllers);
    }

    [Fact]
    public void OldMembershipDisconnectRemovesPunchEndpointFromNewInstance()
    {
        var manager = new MissionManager();
        var peer = CreatePeer(1);
        var netManager = new NetManager(null);
        var internalEndpoint = new IPEndPoint(IPAddress.Loopback, 53007);
        var externalEndpoint = new IPEndPoint(IPAddress.Loopback, 53008);

        Assert.True(manager.TryEnterMission(peer, "moving", "old-instance", out _));
        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            internalEndpoint,
            externalEndpoint,
            "moving%new-instance");

        MissionDeparture departure = Assert.Single(manager.HandleDisconnect(peer));

        Assert.Equal("old-instance", departure.InstanceId);
        Assert.Empty(GetInstance(manager, "new-instance").PunchEndpoints);
    }

    [Fact]
    public void RepunchReplacesEarlierEndpointForController()
    {
        var manager = new MissionManager();
        var netManager = new NetManager(null);
        var oldInternal = new IPEndPoint(IPAddress.Loopback, 53007);
        var oldExternal = new IPEndPoint(IPAddress.Loopback, 53008);
        var replacementInternal = new IPEndPoint(IPAddress.Loopback, 53009);
        var replacementExternal = new IPEndPoint(IPAddress.Loopback, 53010);

        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            oldInternal,
            oldExternal,
            "host%battle");
        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            replacementInternal,
            replacementExternal,
            "host%battle");

        MissionInstance.Endpoints endpoint = Assert.Single(GetInstance(manager, "battle").PunchEndpoints);
        Assert.Equal("host", endpoint.ControllerId);
        Assert.Equal(replacementInternal, endpoint.Internal);
        Assert.Equal(replacementExternal, endpoint.External);
    }

    [Fact]
    public void RepunchStillRemovesMatchingEndpointAcrossInstances()
    {
        var manager = new MissionManager();
        var netManager = new NetManager(null);
        var firstInternal = new IPEndPoint(IPAddress.Loopback, 53011);
        var replacementInternal = new IPEndPoint(IPAddress.Loopback, 53012);
        var sharedExternal = new IPEndPoint(IPAddress.Loopback, 53013);

        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            firstInternal,
            sharedExternal,
            "first%first-battle");
        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            replacementInternal,
            sharedExternal,
            "replacement%replacement-battle");

        Assert.Empty(GetInstance(manager, "first-battle").PunchEndpoints);
        MissionInstance.Endpoints endpoint = Assert.Single(
            GetInstance(manager, "replacement-battle").PunchEndpoints);
        Assert.Equal("replacement", endpoint.ControllerId);
        Assert.Equal(replacementInternal, endpoint.Internal);
        Assert.Equal(sharedExternal, endpoint.External);
    }

    [Fact]
    public void ActiveConclusionClaimFencesLaterEntry()
    {
        var manager = new MissionManager();

        Assert.True(manager.TryEnterMission(CreatePeer(1), "host", "battle", out _));
        Assert.True(manager.TryBeginActiveInstanceConclusion("battle", new[] { "host" }));
        manager.CompleteInstanceConclusion("battle", succeeded: true);
        Assert.False(manager.TryEnterMission(CreatePeer(2), "late", "battle", out _));
    }

    [Fact]
    public void FailedActiveConclusionReopensEntryAndRetry()
    {
        var manager = new MissionManager();

        Assert.True(manager.TryEnterMission(CreatePeer(1), "host", "battle", out _));
        Assert.True(manager.TryBeginActiveInstanceConclusion("battle", new[] { "host" }));
        manager.CompleteInstanceConclusion("battle", succeeded: false);

        Assert.True(manager.TryEnterMission(CreatePeer(2), "late", "battle", out _));
        Assert.True(manager.TryBeginActiveInstanceConclusion("battle", new[] { "host", "late" }));
    }

    [Fact]
    public void ActiveConclusionClaimRejectsChangedMembership()
    {
        var manager = new MissionManager();

        Assert.True(manager.TryEnterMission(CreatePeer(1), "host", "battle", out _));
        Assert.True(manager.TryEnterMission(CreatePeer(2), "late", "battle", out _));

        Assert.False(manager.TryBeginActiveInstanceConclusion("battle", new[] { "host" }));
    }

    [Fact]
    public void DuplicateEntryDoesNotChangeMembership()
    {
        var manager = new MissionManager();
        var peer = CreatePeer(1);

        Assert.True(manager.TryEnterMission(peer, "host", "battle", out _));
        Assert.True(manager.TryEnterMission(peer, "host", "battle", out var duplicate));

        Assert.Equal(MissionEntryStatus.Unchanged, duplicate.Status);
        Assert.Empty(duplicate.ExistingMembers);
        Assert.Empty(duplicate.PreviousDepartures);
        Assert.True(manager.TryGetControllers("battle", out var controllers));
        Assert.Equal(new[] { "host" }, controllers);
    }

    [Fact]
    public void EntryAfterMissedLeaveMovesMembershipAtomically()
    {
        var manager = new MissionManager();
        var movingPeer = CreatePeer(1);
        var survivorPeer = CreatePeer(2);

        Assert.True(manager.TryEnterMission(movingPeer, "moving", "old", out _));
        Assert.True(manager.TryEnterMission(survivorPeer, "survivor", "old", out _));

        Assert.True(manager.TryEnterMission(movingPeer, "moving", "new", out var moved));

        var departure = Assert.Single(moved.PreviousDepartures);
        Assert.Equal("moving", departure.ControllerId);
        Assert.Equal("old", departure.InstanceId);
        Assert.Equal("survivor", Assert.Single(departure.RemainingMembers).controllerId);
        Assert.True(manager.TryGetControllers("old", out var oldControllers));
        Assert.Equal(new[] { "survivor" }, oldControllers);
        Assert.True(manager.TryGetControllers("new", out var newControllers));
        Assert.Equal(new[] { "moving" }, newControllers);
        Assert.False(manager.TryGetRelayTarget(movingPeer, "old", "survivor", out _));
    }

    [Fact]
    public void ReconnectReplacesRouteWithoutLogicalDeparture()
    {
        var manager = new MissionManager();
        var oldPeer = CreatePeer(1);
        var replacementPeer = CreatePeer(2);
        var observerPeer = CreatePeer(3);

        Assert.True(manager.TryEnterMission(oldPeer, "host", "battle", out _));
        Assert.True(manager.TryEnterMission(observerPeer, "observer", "battle", out _));

        Assert.True(manager.TryEnterMission(replacementPeer, "host", "battle", out var replacement));

        Assert.Equal(MissionEntryStatus.Reconnected, replacement.Status);
        Assert.Empty(replacement.PreviousDepartures);
        Assert.Equal("observer", Assert.Single(replacement.ExistingMembers).controllerId);
        Assert.False(manager.TryGetRelayTarget(oldPeer, "battle", "observer", out _));
        Assert.True(manager.TryGetRelayTarget(observerPeer, "battle", "host", out var hostPeer));
        Assert.Same(replacementPeer, hostPeer);
        Assert.Empty(manager.HandleDisconnect(oldPeer));
    }

    [Fact]
    public void DisconnectCleansEveryMembershipTiedToPeer()
    {
        var manager = new MissionManager();
        var peer = CreatePeer(1);
        Assert.True(manager.TryEnterMission(peer, "current", "current-instance", out _));

        var byInstanceIdField = typeof(MissionManager).GetField(
            "byInstanceId",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var instances = (Dictionary<string, MissionInstance>)byInstanceIdField.GetValue(manager)!;
        var staleInstance = new MissionInstance("stale-instance");
        staleInstance.Memberships.Add(new MissionMembership("stale", peer, staleInstance));
        instances[staleInstance.Id] = staleInstance;

        var departures = manager.HandleDisconnect(peer);

        Assert.Equal(2, departures.Count);
        Assert.Contains(departures, departure => departure.ControllerId == "current");
        Assert.Contains(departures, departure => departure.ControllerId == "stale");
        Assert.False(manager.TryGetControllers("current-instance", out _));
        Assert.False(manager.TryGetControllers("stale-instance", out _));
    }

    [Fact]
    public void FailedLeaveReturnsNoDeparture()
    {
        var manager = new MissionManager();
        var peer = CreatePeer(1);

        Assert.True(manager.TryEnterMission(peer, "host", "battle", out _));

        Assert.False(manager.TryLeaveMission(peer, "stale-id", "battle", out _));
        Assert.False(manager.TryLeaveMission(peer, "host", "other", out _));
        Assert.False(manager.TryLeaveMission(CreatePeer(2), "host", "battle", out _));
        Assert.True(manager.TryGetControllers("battle", out var controllers));
        Assert.Equal(new[] { "host" }, controllers);
    }

    [Fact]
    public void RelayRequiresCurrentSourceAndTargetInSameInstance()
    {
        var manager = new MissionManager();
        var source = CreatePeer(1);
        var target = CreatePeer(2);
        var other = CreatePeer(3);

        Assert.True(manager.TryEnterMission(source, "source", "battle", out _));
        Assert.True(manager.TryEnterMission(target, "target", "battle", out _));
        Assert.True(manager.TryEnterMission(other, "other", "other-battle", out _));

        Assert.True(manager.TryGetRelayTarget(source, "battle", "target", out var resolved));
        Assert.Same(target, resolved);
        Assert.False(manager.TryGetRelayTarget(source, "battle", "other", out _));
        Assert.False(manager.TryGetRelayTarget(other, "battle", "target", out _));

        manager.RevokeRelay(target);
        Assert.False(manager.TryGetRelayTarget(source, "battle", "target", out _));
        Assert.True(manager.TryLeaveMission(target, "target", "battle", out _));
        Assert.True(manager.TryEnterMission(target, "target", "battle", out _));

        manager.RevokeRelay(source);
        Assert.False(manager.TryGetRelayTarget(source, "battle", "target", out _));
        Assert.True(manager.TryLeaveMission(source, "source", "battle", out _));
        Assert.False(manager.TryGetRelayTarget(source, "battle", "target", out _));
    }

    [Fact]
    public void FailedLeaveClearsItsTemporaryPeerFence()
    {
        var manager = new MissionManager();
        var source = CreatePeer(1);
        var target = CreatePeer(2);

        Assert.True(manager.TryEnterMission(source, "source", "battle", out _));
        Assert.True(manager.TryEnterMission(target, "target", "battle", out _));

        manager.RevokeRelay(source);
        Assert.False(manager.TryGetRelayTarget(source, "battle", "target", out _));
        Assert.False(manager.TryLeaveMission(source, "source", string.Empty, out _));
        Assert.True(manager.TryGetRelayTarget(source, "battle", "target", out _));
    }

    [Fact]
    public void EarlierFailedLeaveDoesNotClearLaterLeaveFence()
    {
        var manager = new MissionManager();
        var source = CreatePeer(1);
        var target = CreatePeer(2);

        Assert.True(manager.TryEnterMission(source, "source", "battle", out _));
        Assert.True(manager.TryEnterMission(target, "target", "battle", out _));

        manager.RevokeRelay(source);
        manager.RevokeRelay(source);
        Assert.False(manager.TryLeaveMission(source, "source", "stale-battle", out _));

        Assert.False(manager.TryGetRelayTarget(source, "battle", "target", out _));
        Assert.True(manager.TryLeaveMission(source, "source", "battle", out _));
    }

    [Fact]
    public void DisconnectRevocationDoesNotTransferToReconnectReplacement()
    {
        var manager = new MissionManager();
        var oldPeer = CreatePeer(1);
        var replacementPeer = CreatePeer(2);
        var observerPeer = CreatePeer(3);

        Assert.True(manager.TryEnterMission(oldPeer, "host", "battle", out _));
        Assert.True(manager.TryEnterMission(observerPeer, "observer", "battle", out _));

        manager.RevokeRelay(oldPeer);
        Assert.True(manager.TryEnterMission(replacementPeer, "host", "battle", out _));
        Assert.Empty(manager.HandleDisconnect(oldPeer));

        Assert.True(manager.TryGetRelayTarget(observerPeer, "battle", "host", out var resolved));
        Assert.Same(replacementPeer, resolved);
    }

    private static MissionInstance GetInstance(MissionManager manager, string instanceId)
    {
        var byInstanceIdField = typeof(MissionManager).GetField(
            "byInstanceId",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var instances = (Dictionary<string, MissionInstance>)byInstanceIdField.GetValue(manager)!;
        return instances[instanceId];
    }

    private static NetPeer CreatePeer(int id)
        => (NetPeer)PeerConstructor.Invoke(new object[]
        {
            new NetManager(null),
            new IPEndPoint(IPAddress.Loopback, 52000 + id),
            id,
        });
}
