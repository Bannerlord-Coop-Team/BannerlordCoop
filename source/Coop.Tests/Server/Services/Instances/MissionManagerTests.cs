using Coop.Core.Server.Services.Instances;
using GameInterface.Services.Players;
using LiteNetLib;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
        var manager = CreateManager();
        var first = CreatePeer(1);
        var second = CreatePeer(2);

        Assert.True(manager.TryEnterMission(first, "first", "battle", out var firstEntry));
        Assert.True(firstEntry.IsFirstMember);
        Assert.Empty(firstEntry.ExistingMembers);
        Assert.NotEqual(Guid.Empty, firstEntry.PeerCredential);

        Assert.True(manager.TryEnterMission(second, "second", "battle", out var secondEntry));
        Assert.False(secondEntry.IsFirstMember);
        var existing = Assert.Single(secondEntry.ExistingMembers);
        Assert.Equal(firstEntry.PeerCredential, existing.peerCredential);
        Assert.NotEqual(firstEntry.PeerCredential, secondEntry.PeerCredential);
    }

    [Fact]
    public void EmptyConclusionClaimRejectsReentrantEntry()
    {
        var manager = CreateManager();

        Assert.True(manager.TryBeginEmptyInstanceConclusion("battle"));
        Assert.False(manager.TryEnterMission(CreatePeer(1), "late", "battle", out _));
        manager.CompleteInstanceConclusion("battle", succeeded: true);
    }

    [Fact]
    public void NatOnlyShellDoesNotBlockEmptyConclusionClaim()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("late", peer));
        var netManager = new NetManager(null);
        var local = new IPEndPoint(IPAddress.Loopback, 53001);
        var remote = new IPEndPoint(IPAddress.Loopback, 53002);

        manager.HandleIntroductionRequest(netManager.NatPunchModule, local, remote, Authorize(manager, peer, "late", "battle"));

        Assert.False(manager.TryGetControllers("battle", out _));
        Assert.True(manager.TryBeginEmptyInstanceConclusion("battle"));
        manager.CompleteInstanceConclusion("battle", succeeded: true);
        Assert.False(manager.TryEnterMission(peer, "late", "battle", out _));
    }

    [Fact]
    public void FailedConclusionRestoresNatOnlyShell()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("late", peer));
        var netManager = new NetManager(null);
        var local = new IPEndPoint(IPAddress.Loopback, 53003);
        var remote = new IPEndPoint(IPAddress.Loopback, 53004);

        manager.HandleIntroductionRequest(netManager.NatPunchModule, local, remote, Authorize(manager, peer, "late", "battle"));

        Assert.True(manager.TryBeginEmptyInstanceConclusion("battle"));
        manager.CompleteInstanceConclusion("battle", succeeded: false);
        Assert.True(manager.TryEnterMission(peer, "late", "battle", out _));
    }

    [Fact]
    public void FailedConclusionDoesNotRestoreControllerEndpointReplacedInAnotherInstance()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        var netManager = new NetManager(null);
        var oldInternal = new IPEndPoint(IPAddress.Loopback, 53005);
        var oldExternal = new IPEndPoint(IPAddress.Loopback, 53006);
        var replacementInternal = new IPEndPoint(IPAddress.Loopback, 53007);
        var replacementExternal = new IPEndPoint(IPAddress.Loopback, 53008);

        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            oldInternal,
            oldExternal,
            Authorize(manager, peer, "moving", "old-instance"));
        Assert.True(manager.TryBeginEmptyInstanceConclusion("old-instance"));

        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            replacementInternal,
            replacementExternal,
            Authorize(manager, peer, "moving", "new-instance"));
        Assert.True(manager.CompleteInstanceConclusion("old-instance", succeeded: false));

        Assert.Empty(GetInstance(manager, "old-instance").PunchEndpoints);
        MissionInstance.Endpoints endpoint = Assert.Single(
            GetInstance(manager, "new-instance").PunchEndpoints);
        Assert.Equal(replacementExternal, endpoint.External);
    }

    [Fact]
    public void FailedConclusionDoesNotRestoreEndpointReplacedByExternalAddress()
    {
        var oldPeer = CreatePeer(1);
        var replacementPeer = CreatePeer(2);
        var manager = CreateManager(("old", oldPeer), ("replacement", replacementPeer));
        var netManager = new NetManager(null);
        var oldInternal = new IPEndPoint(IPAddress.Loopback, 53009);
        var replacementInternal = new IPEndPoint(IPAddress.Loopback, 53010);
        var sharedExternal = new IPEndPoint(IPAddress.Loopback, 53011);

        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            oldInternal,
            sharedExternal,
            Authorize(manager, oldPeer, "old", "old-instance"));
        Assert.True(manager.TryBeginEmptyInstanceConclusion("old-instance"));

        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            replacementInternal,
            sharedExternal,
            Authorize(manager, replacementPeer, "replacement", "new-instance"));
        Assert.True(manager.CompleteInstanceConclusion("old-instance", succeeded: false));

        Assert.Empty(GetInstance(manager, "old-instance").PunchEndpoints);
        MissionInstance.Endpoints endpoint = Assert.Single(
            GetInstance(manager, "new-instance").PunchEndpoints);
        Assert.Equal("replacement", endpoint.ControllerId);
    }

    [Fact]
    public void GracefulLeaveRemovesControllerPunchEndpoint()
    {
        var departingPeer = CreatePeer(1);
        var survivorPeer = CreatePeer(2);
        var manager = CreateManager(("departing", departingPeer), ("survivor", survivorPeer));
        var netManager = new NetManager(null);
        var internalEndpoint = new IPEndPoint(IPAddress.Loopback, 53005);
        var externalEndpoint = new IPEndPoint(IPAddress.Loopback, 53006);

        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            internalEndpoint,
            externalEndpoint,
            Authorize(manager, departingPeer, "departing", "battle"));
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
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        var netManager = new NetManager(null);
        var internalEndpoint = new IPEndPoint(IPAddress.Loopback, 53007);
        var externalEndpoint = new IPEndPoint(IPAddress.Loopback, 53008);

        Assert.True(manager.TryEnterMission(peer, "moving", "old-instance", out _));
        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            internalEndpoint,
            externalEndpoint,
            Authorize(manager, peer, "moving", "new-instance"));

        MissionDeparture departure = Assert.Single(manager.HandleDisconnect(peer));

        Assert.Equal("old-instance", departure.InstanceId);
        Assert.Empty(GetInstance(manager, "new-instance").PunchEndpoints);
    }

    [Fact]
    public void OldMembershipPunchNewThenEnterNewRetainsNewPunchEndpoint()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        var netManager = new NetManager(null);
        var internalEndpoint = new IPEndPoint(IPAddress.Loopback, 53009);
        var externalEndpoint = new IPEndPoint(IPAddress.Loopback, 53010);

        Assert.True(manager.TryEnterMission(peer, "moving", "old-instance", out _));
        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            internalEndpoint,
            externalEndpoint,
            Authorize(manager, peer, "moving", "new-instance"));

        Assert.True(manager.TryEnterMission(peer, "moving", "new-instance", out var entry));

        Assert.Equal("old-instance", Assert.Single(entry.PreviousDepartures).InstanceId);
        MissionInstance.Endpoints endpoint = Assert.Single(
            GetInstance(manager, "new-instance").PunchEndpoints);
        Assert.Equal("moving", endpoint.ControllerId);
        Assert.Equal(internalEndpoint, endpoint.Internal);
        Assert.Equal(externalEndpoint, endpoint.External);
    }

    [Fact]
    public void OldMembershipPunchNewThenGracefulLeaveRetainsNewPunchEndpoint()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        var netManager = new NetManager(null);
        var internalEndpoint = new IPEndPoint(IPAddress.Loopback, 53011);
        var externalEndpoint = new IPEndPoint(IPAddress.Loopback, 53012);

        Assert.True(manager.TryEnterMission(peer, "moving", "old-instance", out _));
        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            internalEndpoint,
            externalEndpoint,
            Authorize(manager, peer, "moving", "new-instance"));

        Assert.True(manager.TryLeaveMission(peer, "moving", "old-instance", out _));

        MissionInstance.Endpoints endpoint = Assert.Single(
            GetInstance(manager, "new-instance").PunchEndpoints);
        Assert.Equal("moving", endpoint.ControllerId);
        Assert.Equal(internalEndpoint, endpoint.Internal);
        Assert.Equal(externalEndpoint, endpoint.External);
    }

    [Fact]
    public void OldPeerDisconnectDoesNotRemoveReplacementPeerPunchEndpoint()
    {
        var oldPeer = CreatePeer(1);
        var replacementPeer = CreatePeer(2);
        var manager = CreateManager(("moving", replacementPeer));
        var netManager = new NetManager(null);
        var internalEndpoint = new IPEndPoint(IPAddress.Loopback, 53013);
        var externalEndpoint = new IPEndPoint(IPAddress.Loopback, 53014);

        Assert.True(manager.TryEnterMission(oldPeer, "moving", "old-instance", out _));
        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            internalEndpoint,
            externalEndpoint,
            Authorize(manager, replacementPeer, "moving", "new-instance"));

        Assert.Single(manager.HandleDisconnect(oldPeer));

        MissionInstance.Endpoints endpoint = Assert.Single(
            GetInstance(manager, "new-instance").PunchEndpoints);
        Assert.Same(replacementPeer, endpoint.CampaignPeer);
        Assert.Equal(internalEndpoint, endpoint.Internal);
        Assert.Equal(externalEndpoint, endpoint.External);
    }

    [Fact]
    public void GracefulLeaveThenDisconnectBeforeEntryRemovesPunchEndpoint()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        var netManager = new NetManager(null);
        var internalEndpoint = new IPEndPoint(IPAddress.Loopback, 53015);
        var externalEndpoint = new IPEndPoint(IPAddress.Loopback, 53016);

        Assert.True(manager.TryEnterMission(peer, "moving", "old-instance", out _));
        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            internalEndpoint,
            externalEndpoint,
            Authorize(manager, peer, "moving", "new-instance"));
        Assert.True(manager.TryLeaveMission(peer, "moving", "old-instance", out _));

        Assert.Empty(manager.HandleDisconnect(peer));

        Assert.Empty(GetInstance(manager, "new-instance").PunchEndpoints);
    }

    [Fact]
    public void LastMemberDisconnectRetainsAnotherControllersPreEntryPunch()
    {
        var departingPeer = CreatePeer(1);
        var enteringPeer = CreatePeer(2);
        var manager = CreateManager(("entering", enteringPeer));
        var netManager = new NetManager(null);
        var internalEndpoint = new IPEndPoint(IPAddress.Loopback, 53017);
        var externalEndpoint = new IPEndPoint(IPAddress.Loopback, 53018);

        Assert.True(manager.TryEnterMission(departingPeer, "departing", "battle", out _));
        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            internalEndpoint,
            externalEndpoint,
            Authorize(manager, enteringPeer, "entering", "battle"));

        Assert.Single(manager.HandleDisconnect(departingPeer));

        MissionInstance.Endpoints endpoint = Assert.Single(GetInstance(manager, "battle").PunchEndpoints);
        Assert.Equal("entering", endpoint.ControllerId);
        Assert.Same(enteringPeer, endpoint.CampaignPeer);
        Assert.True(manager.TryEnterMission(enteringPeer, "entering", "battle", out _));
        Assert.Single(GetInstance(manager, "battle").PunchEndpoints);
    }

    [Fact]
    public void LastMemberLeaveWithoutPunchPrunesInstance()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager();

        Assert.True(manager.TryEnterMission(peer, "departing", "battle", out _));
        Assert.True(manager.TryLeaveMission(peer, "departing", "battle", out _));

        Assert.False(HasInstance(manager, "battle"));
    }

    [Fact]
    public void RepunchReplacesEarlierEndpointForController()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("host", peer));
        var netManager = new NetManager(null);
        var oldInternal = new IPEndPoint(IPAddress.Loopback, 53007);
        var oldExternal = new IPEndPoint(IPAddress.Loopback, 53008);
        var replacementInternal = new IPEndPoint(IPAddress.Loopback, 53009);
        var replacementExternal = new IPEndPoint(IPAddress.Loopback, 53010);

        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            oldInternal,
            oldExternal,
            Authorize(manager, peer, "host", "battle"));
        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            replacementInternal,
            replacementExternal,
            Authorize(manager, peer, "host", "battle"));

        MissionInstance.Endpoints endpoint = Assert.Single(GetInstance(manager, "battle").PunchEndpoints);
        Assert.Equal("host", endpoint.ControllerId);
        Assert.Equal(replacementInternal, endpoint.Internal);
        Assert.Equal(replacementExternal, endpoint.External);
    }

    [Fact]
    public void RepunchStillRemovesMatchingEndpointAcrossInstances()
    {
        var firstPeer = CreatePeer(1);
        var replacementPeer = CreatePeer(2);
        var manager = CreateManager(("first", firstPeer), ("replacement", replacementPeer));
        var netManager = new NetManager(null);
        var firstInternal = new IPEndPoint(IPAddress.Loopback, 53011);
        var replacementInternal = new IPEndPoint(IPAddress.Loopback, 53012);
        var sharedExternal = new IPEndPoint(IPAddress.Loopback, 53013);

        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            firstInternal,
            sharedExternal,
            Authorize(manager, firstPeer, "first", "first-battle"));
        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            replacementInternal,
            sharedExternal,
            Authorize(manager, replacementPeer, "replacement", "replacement-battle"));

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
        var manager = CreateManager();

        Assert.True(manager.TryEnterMission(CreatePeer(1), "host", "battle", out _));
        Assert.True(manager.TryBeginActiveInstanceConclusion("battle", new[] { "host" }));
        manager.CompleteInstanceConclusion("battle", succeeded: true);
        Assert.False(manager.TryEnterMission(CreatePeer(2), "late", "battle", out _));
    }

    [Fact]
    public void FailedActiveConclusionReopensEntryAndRetry()
    {
        var manager = CreateManager();

        Assert.True(manager.TryEnterMission(CreatePeer(1), "host", "battle", out _));
        Assert.True(manager.TryBeginActiveInstanceConclusion("battle", new[] { "host" }));
        manager.CompleteInstanceConclusion("battle", succeeded: false);

        Assert.True(manager.TryEnterMission(CreatePeer(2), "late", "battle", out _));
        Assert.True(manager.TryBeginActiveInstanceConclusion("battle", new[] { "host", "late" }));
    }

    [Fact]
    public void ActiveConclusionClaimRejectsChangedMembership()
    {
        var manager = CreateManager();

        Assert.True(manager.TryEnterMission(CreatePeer(1), "host", "battle", out _));
        Assert.True(manager.TryEnterMission(CreatePeer(2), "late", "battle", out _));

        Assert.False(manager.TryBeginActiveInstanceConclusion("battle", new[] { "host" }));
    }

    [Fact]
    public void DuplicateEntryDoesNotChangeMembership()
    {
        var manager = CreateManager();
        var peer = CreatePeer(1);
        var observerPeer = CreatePeer(2);

        Assert.True(manager.TryEnterMission(peer, "host", "battle", out var first));
        Assert.True(manager.TryEnterMission(observerPeer, "observer", "battle", out var observer));
        Assert.True(manager.TryEnterMission(peer, "host", "battle", out var duplicate));

        Assert.Equal(MissionEntryStatus.Unchanged, duplicate.Status);
        var existing = Assert.Single(duplicate.ExistingMembers);
        Assert.Equal("observer", existing.controllerId);
        Assert.Same(observerPeer, existing.peer);
        Assert.Equal(observer.PeerCredential, existing.peerCredential);
        Assert.Empty(duplicate.PreviousDepartures);
        Assert.Equal(first.PeerCredential, duplicate.PeerCredential);
        Assert.True(manager.TryGetControllers("battle", out var controllers));
        Assert.Equal(new[] { "host", "observer" }, controllers.OrderBy(value => value));
    }

    [Fact]
    public void EntryAfterMissedLeaveMovesMembershipAtomically()
    {
        var manager = CreateManager();
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
        var manager = CreateManager();
        var oldPeer = CreatePeer(1);
        var replacementPeer = CreatePeer(2);
        var observerPeer = CreatePeer(3);

        Assert.True(manager.TryEnterMission(oldPeer, "host", "battle", out var original));
        Assert.True(manager.TryEnterMission(observerPeer, "observer", "battle", out _));

        Assert.True(manager.TryEnterMission(replacementPeer, "host", "battle", out var replacement));

        Assert.Equal(MissionEntryStatus.Reconnected, replacement.Status);
        Assert.NotEqual(original.PeerCredential, replacement.PeerCredential);
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
        var manager = CreateManager();
        var peer = CreatePeer(1);
        Assert.True(manager.TryEnterMission(peer, "current", "current-instance", out _));

        var byInstanceIdField = typeof(MissionManager).GetField(
            "byInstanceId",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var instances = (Dictionary<string, MissionInstance>)byInstanceIdField.GetValue(manager)!;
        var staleInstance = new MissionInstance("stale-instance");
        staleInstance.Memberships.Add(new MissionMembership(
            "stale",
            peer,
            staleInstance,
            Guid.NewGuid()));
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
        var manager = CreateManager();
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
        var manager = CreateManager();
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
        var manager = CreateManager();
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
        var manager = CreateManager();
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
        var manager = CreateManager();
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

    [Fact]
    public async Task DisconnectCompletesBeforeBlockedPunchCanInsert()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        var token = Authorize(manager, peer, "moving", "battle");
        var nat = new NetManager(null).NatPunchModule;
        var endpoint = new IPEndPoint(IPAddress.Loopback, 53001);
        var gate = typeof(MissionManager).GetField("gate", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;
        using var started = new ManualResetEventSlim();
        Task punch;

        lock (gate)
        {
            punch = Task.Run(() =>
            {
                started.Set();
                manager.HandleIntroductionRequest(nat, endpoint, endpoint, token);
            });
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            Assert.Empty(manager.HandleDisconnect(peer));
        }

        await punch.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(HasInstance(manager, "battle"));
        // The player registry may still expose this peer after mission cleanup has completed.
        Assert.False(manager.TryAuthorizeIntroduction(peer, "moving", "battle", Guid.NewGuid(), out _));
    }

    [Fact]
    public void IntroductionResolvesCurrentPeerInsideTheDisconnectGate()
    {
        var peer = CreatePeer(1);
        var playerManager = new Mock<IPlayerManager>();
        var manager = new MissionManager(playerManager.Object);
        var gate = typeof(MissionManager).GetField("gate", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;
        playerManager.Setup(p => p.TryGetPeer("moving", out It.Ref<NetPeer>.IsAny))
            .Returns((string _, out NetPeer currentPeer) =>
            {
                Assert.True(Monitor.IsEntered(gate));
                currentPeer = peer;
                return true;
            });
        var token = Authorize(manager, peer, "moving", "battle");
        var endpoint = new IPEndPoint(IPAddress.Loopback, 53001);

        manager.HandleIntroductionRequest(new NetManager(null).NatPunchModule, endpoint, endpoint, token);

        Assert.Single(GetInstance(manager, "battle").PunchEndpoints);
        manager.HandleDisconnect(peer);
        Assert.Empty(GetInstance(manager, "battle").PunchEndpoints);
    }

    [Fact]
    public void DelayedOldSessionPunchCannotOverwriteReplacementEndpoint()
    {
        var oldPeer = CreatePeer(1);
        var replacementPeer = CreatePeer(2);
        var playerManager = new Mock<IPlayerManager>();
        var currentPeer = oldPeer;
        playerManager.Setup(p => p.TryGetPeer("moving", out It.Ref<NetPeer>.IsAny))
            .Returns((string _, out NetPeer peer) => { peer = currentPeer; return true; });
        var manager = new MissionManager(playerManager.Object);
        var oldToken = Authorize(manager, oldPeer, "moving", "battle");
        currentPeer = replacementPeer;
        var replacementToken = Authorize(manager, replacementPeer, "moving", "battle");
        var nat = new NetManager(null).NatPunchModule;
        var oldEndpoint = new IPEndPoint(IPAddress.Loopback, 53001);
        var replacementEndpoint = new IPEndPoint(IPAddress.Loopback, 53002);

        manager.HandleIntroductionRequest(nat, replacementEndpoint, replacementEndpoint, replacementToken);
        manager.HandleDisconnect(oldPeer);
        manager.HandleIntroductionRequest(nat, oldEndpoint, oldEndpoint, oldToken);

        var endpoint = Assert.Single(GetInstance(manager, "battle").PunchEndpoints);
        Assert.Same(replacementPeer, endpoint.CampaignPeer);
        Assert.Equal(replacementEndpoint, endpoint.External);
        Assert.False(manager.TryAuthorizeIntroduction(oldPeer, "moving", "battle", Guid.NewGuid(), out _));
    }

    [Fact]
    public void ReplacementRegistrationRejectsOldPunchBeforeReplacementAuthorization()
    {
        var oldPeer = CreatePeer(1);
        var replacementPeer = CreatePeer(2);
        var playerManager = new Mock<IPlayerManager>();
        var currentPeer = oldPeer;
        playerManager.Setup(p => p.TryGetPeer("moving", out It.Ref<NetPeer>.IsAny))
            .Returns((string _, out NetPeer peer) => { peer = currentPeer; return true; });
        var manager = new MissionManager(playerManager.Object);
        var token = Authorize(manager, oldPeer, "moving", "battle");
        currentPeer = replacementPeer;
        var endpoint = new IPEndPoint(IPAddress.Loopback, 53001);

        manager.HandleIntroductionRequest(new NetManager(null).NatPunchModule, endpoint, endpoint, token);

        Assert.False(HasInstance(manager, "battle"));
        Assert.False(manager.TryAuthorizeIntroduction(oldPeer, "moving", "battle", Guid.NewGuid(), out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateRequestAndPunchRetainTheFirstAcceptedEndpoint(bool member)
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        if (member) Assert.True(manager.TryEnterMission(peer, "moving", "battle", out _));
        var requestId = Guid.NewGuid();
        Assert.True(manager.TryAuthorizeIntroduction(peer, "moving", "battle", requestId, out var token));
        var nat = new NetManager(null).NatPunchModule;
        var firstEndpoint = new IPEndPoint(IPAddress.Loopback, 53001);
        var delayedEndpoint = new IPEndPoint(IPAddress.Loopback, 53002);
        manager.HandleIntroductionRequest(nat, firstEndpoint, firstEndpoint, token);

        Assert.True(manager.TryAuthorizeIntroduction(peer, "moving", "battle", requestId, out var duplicateToken));
        Assert.Equal(token, duplicateToken);
        manager.HandleIntroductionRequest(nat, delayedEndpoint, delayedEndpoint, duplicateToken);

        Assert.Equal(firstEndpoint, Assert.Single(GetInstance(manager, "battle").PunchEndpoints).External);
    }

    [Fact]
    public void SupersededRequestCannotInsertAnEndpoint()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        var oldToken = Authorize(manager, peer, "moving", "old-instance");
        var newToken = Authorize(manager, peer, "moving", "new-instance");
        var nat = new NetManager(null).NatPunchModule;
        var endpoint = new IPEndPoint(IPAddress.Loopback, 53001);

        manager.HandleIntroductionRequest(nat, endpoint, endpoint, oldToken);
        manager.HandleIntroductionRequest(nat, endpoint, endpoint, newToken);

        Assert.False(HasInstance(manager, "old-instance"));
        Assert.Single(GetInstance(manager, "new-instance").PunchEndpoints);
    }

    [Fact]
    public void GracefulLeaveInvalidatesUnconsumedAuthorizationForDepartedInstance()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        var token = Authorize(manager, peer, "moving", "battle");
        Assert.True(manager.TryEnterMission(peer, "moving", "battle", out _));
        Assert.True(manager.TryLeaveMission(peer, "moving", "battle", out _));
        var endpoint = new IPEndPoint(IPAddress.Loopback, 53001);

        manager.HandleIntroductionRequest(new NetManager(null).NatPunchModule, endpoint, endpoint, token);

        Assert.False(HasInstance(manager, "battle"));
    }

    [Theory]
    [InlineData("moving%battle")]
    [InlineData("invalid")]
    [InlineData("00000000000000000000000000000000")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public void UnissuedTokenCannotCreateNatShell(string token)
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        var endpoint = new IPEndPoint(IPAddress.Loopback, 53001);

        manager.HandleIntroductionRequest(new NetManager(null).NatPunchModule, endpoint, endpoint, token);

        Assert.False(HasInstance(manager, "battle"));
    }

    [Fact]
    public void MembershipEntryRotatesAnEarlierAuthorizationEvenForTheSameRequest()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        var requestId = Guid.NewGuid();
        Assert.True(manager.TryAuthorizeIntroduction(peer, "moving", "battle", requestId, out var oldToken));
        Assert.True(manager.TryEnterMission(peer, "moving", "battle", out _));

        Assert.True(manager.TryAuthorizeIntroduction(peer, "moving", "battle", requestId, out var currentToken));
        Assert.NotEqual(oldToken, currentToken);
        var nat = new NetManager(null).NatPunchModule;
        var endpoint = new IPEndPoint(IPAddress.Loopback, 53001);
        manager.HandleIntroductionRequest(nat, endpoint, endpoint, oldToken);
        Assert.Empty(GetInstance(manager, "battle").PunchEndpoints);

        manager.HandleIntroductionRequest(nat, endpoint, endpoint, currentToken);
        Assert.Single(GetInstance(manager, "battle").PunchEndpoints);
    }

    [Fact]
    public void ReplacementMembershipRejectsOldAuthorizationBeforePlayerRegistryChanges()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        Assert.True(manager.TryEnterMission(peer, "moving", "battle", out var original));
        var token = Authorize(manager, peer, "moving", "battle");
        Assert.True(manager.TryEnterMission(CreatePeer(2), "moving", "battle", out var replacement));
        Assert.NotEqual(original.PeerCredential, replacement.PeerCredential);
        var endpoint = new IPEndPoint(IPAddress.Loopback, 53001);

        manager.HandleIntroductionRequest(new NetManager(null).NatPunchModule, endpoint, endpoint, token);

        Assert.Empty(GetInstance(manager, "battle").PunchEndpoints);
    }

    [Fact]
    public void SameInstanceReentryRejectsThePreviousCredentialAuthorization()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        Assert.True(manager.TryEnterMission(peer, "moving", "battle", out var original));
        var requestId = Guid.NewGuid();
        Assert.True(manager.TryAuthorizeIntroduction(peer, "moving", "battle", requestId, out var oldToken));
        Assert.True(manager.TryLeaveMission(peer, "moving", "battle", out _));
        Assert.True(manager.TryEnterMission(peer, "moving", "battle", out var current));
        Assert.NotEqual(original.PeerCredential, current.PeerCredential);
        Assert.True(manager.TryAuthorizeIntroduction(peer, "moving", "battle", requestId, out var currentToken));
        Assert.NotEqual(oldToken, currentToken);
        var nat = new NetManager(null).NatPunchModule;
        var currentEndpoint = new IPEndPoint(IPAddress.Loopback, 53001);
        var staleEndpoint = new IPEndPoint(IPAddress.Loopback, 53002);

        manager.HandleIntroductionRequest(nat, currentEndpoint, currentEndpoint, currentToken);
        manager.HandleIntroductionRequest(nat, staleEndpoint, staleEndpoint, oldToken);

        Assert.Equal(currentEndpoint, Assert.Single(GetInstance(manager, "battle").PunchEndpoints).External);
    }

    [Fact]
    public void MemberAuthorizationReservesTheCredentialInTheDiscoveryTokenLengthLimit()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        string longestInstance = new string('x', NatPunchModule.MaxTokenLength - "moving%".Length - 33);
        Assert.True(manager.TryEnterMission(peer, "moving", longestInstance, out _));

        Assert.True(manager.TryAuthorizeIntroduction(peer, "moving", longestInstance, Guid.NewGuid(), out var token));
        Assert.True(Guid.TryParseExact(token, "N", out _));

        Assert.True(manager.TryEnterMission(peer, "moving", longestInstance + "x", out _));
        Assert.False(manager.TryAuthorizeIntroduction(peer, "moving", longestInstance + "x", Guid.NewGuid(), out _));
    }

    [Fact]
    public void AuthorizationHonorsLiteNetLibDiscoveryTokenLengthLimit()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        string longestInstance = new string('x', NatPunchModule.MaxTokenLength - "moving%".Length);

        Assert.True(manager.TryAuthorizeIntroduction(peer, "moving", longestInstance, Guid.NewGuid(), out var token));
        Assert.True(Guid.TryParseExact(token, "N", out _));
        Assert.False(manager.TryAuthorizeIntroduction(peer, "moving", longestInstance + "x", Guid.NewGuid(), out _));
    }

    [Fact]
    public void DisconnectedNatOnlyInstanceIsRemovedByTheMaintenancePass()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("late", peer));

        Punch(manager, peer, "late", "battle", 53101);
        Assert.True(HasInstance(manager, "battle"));

        // A disconnect drops the endpoints but leaves the instance behind, and without endpoints its
        // lease can no longer remove it.
        Assert.Empty(manager.HandleDisconnect(peer));
        manager.PruneExpired(DateTime.UtcNow);

        Assert.False(HasInstance(manager, "battle"));
        Assert.Equal(0, manager.GetDiagnostics().ActiveInstances);
    }

    [Fact]
    public void FirstNatPunchCreatesTheInstance()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("late", peer));

        Punch(manager, peer, "late", "battle", 53201);

        Assert.True(HasInstance(manager, "battle"));
        var counts = manager.GetDiagnostics();
        Assert.Equal(1, counts.ActiveInstances);
        Assert.Equal(1, counts.NatOnlyInstances);
        Assert.Equal(0, counts.MembershipBackedInstances);
        Assert.Equal(1, counts.PunchEndpoints);
    }

    [Fact]
    public void NatOnlyInstanceSurvivesUntilItsLeaseExpires()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("late", peer));
        var punchedUtc = DateTime.UtcNow;

        Punch(manager, peer, "late", "battle", 53203);
        manager.PruneExpired(punchedUtc + TimeSpan.FromMinutes(4));

        Assert.True(HasInstance(manager, "battle"));
        Assert.Equal(1, manager.GetDiagnostics().PunchEndpoints);
    }

    [Fact]
    public void NatOnlyInstanceAndItsEndpointsExpireAfterTheLease()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("late", peer));
        var punchedUtc = DateTime.UtcNow;

        Punch(manager, peer, "late", "battle", 53205);
        manager.PruneExpired(punchedUtc + TimeSpan.FromMinutes(6));

        Assert.False(HasInstance(manager, "battle"));
        var counts = manager.GetDiagnostics();
        Assert.Equal(0, counts.ActiveInstances);
        Assert.Equal(0, counts.PunchEndpoints);
        Assert.True(counts.ExpiredEntries > 0);
    }

    [Fact]
    public void MembershipKeepsItsInstanceAndEndpointThroughExpiry()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        var punchedUtc = DateTime.UtcNow;

        Assert.True(manager.TryEnterMission(peer, "moving", "battle", out _));
        Punch(manager, peer, "moving", "battle", 53207);
        manager.PruneExpired(punchedUtc + TimeSpan.FromHours(1));

        Assert.True(HasInstance(manager, "battle"));
        var counts = manager.GetDiagnostics();
        Assert.Equal(1, counts.MembershipBackedInstances);
        Assert.Equal(1, counts.PunchEndpoints);
    }

    [Fact]
    public void LastMembershipLeavingStillPrunesImmediately()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));

        Assert.True(manager.TryEnterMission(peer, "moving", "battle", out _));
        Assert.True(manager.TryLeaveMission(peer, "moving", "battle", out _));

        Assert.False(HasInstance(manager, "battle"));
        Assert.Equal(0, manager.GetDiagnostics().ActiveInstances);
    }

    [Fact]
    public void SuccessfulConclusionRejectsADelayedDuplicate()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("late", peer));
        var concludedUtc = DateTime.UtcNow;

        Assert.True(manager.TryBeginEmptyInstanceConclusion("battle"));
        Assert.True(manager.CompleteInstanceConclusion("battle", succeeded: true));
        Assert.Equal(1, manager.GetDiagnostics().ConclusionTombstones);

        manager.PruneExpired(concludedUtc + TimeSpan.FromMinutes(14));

        Assert.False(manager.TryEnterMission(peer, "late", "battle", out _));
        Assert.False(manager.TryAuthorizeIntroduction(peer, "late", "battle", Guid.NewGuid(), out _));
    }

    [Fact]
    public void ConclusionProtectionIsReleasedAfterItsRetention()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("late", peer));
        var concludedUtc = DateTime.UtcNow;

        Assert.True(manager.TryBeginEmptyInstanceConclusion("battle"));
        Assert.True(manager.CompleteInstanceConclusion("battle", succeeded: true));

        manager.PruneExpired(concludedUtc + TimeSpan.FromMinutes(16));

        Assert.Equal(0, manager.GetDiagnostics().ConclusionTombstones);
        Assert.True(manager.TryEnterMission(peer, "late", "battle", out _));
    }

    [Fact]
    public void ConclusionProtectionStaysWithinItsBound()
    {
        var manager = CreateManager();

        for (int index = 0; index < 5000; index++)
        {
            string instanceId = "battle-" + index;
            Assert.True(manager.TryBeginEmptyInstanceConclusion(instanceId));
            Assert.True(manager.CompleteInstanceConclusion(instanceId, succeeded: true));
        }

        var counts = manager.GetDiagnostics();
        Assert.True(counts.ConclusionTombstones <= 4096);
        Assert.True(counts.ExpiredEntries >= 5000 - 4096);
    }

    [Fact]
    public void StalledEmptyConclusionRollsBackAfterItsDeadline()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("late", peer));
        var startedUtc = DateTime.UtcNow;

        Assert.True(manager.TryBeginEmptyInstanceConclusion("battle"));
        manager.PruneExpired(startedUtc + TimeSpan.FromMinutes(6));

        Assert.Equal(0, manager.GetDiagnostics().ConcludingInstances);
        Assert.Equal(0, manager.GetDiagnostics().ConclusionTombstones);
        Assert.True(manager.TryEnterMission(peer, "late", "battle", out _));
    }

    [Fact]
    public void StalledActiveConclusionRollsBackAndKeepsItsMembers()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("moving", peer));
        var startedUtc = DateTime.UtcNow;

        Assert.True(manager.TryEnterMission(peer, "moving", "battle", out _));
        Assert.True(manager.TryBeginActiveInstanceConclusion("battle", new[] { "moving" }));

        manager.PruneExpired(startedUtc + TimeSpan.FromMinutes(6));

        Assert.True(HasInstance(manager, "battle"));
        Assert.True(manager.TryGetControllers("battle", out var controllers));
        Assert.Equal(new[] { "moving" }, controllers);
    }

    [Fact]
    public void ExpiryDoesNotReopenRelayForARevokedPeerOrAConcludedInstance()
    {
        var first = CreatePeer(1);
        var second = CreatePeer(2);
        var manager = CreateManager(("first", first), ("second", second));
        var startedUtc = DateTime.UtcNow;

        Assert.True(manager.TryEnterMission(first, "first", "battle", out _));
        Assert.True(manager.TryEnterMission(second, "second", "battle", out _));
        Assert.True(manager.TryGetRelayTarget(first, "battle", "second", out _));

        manager.RevokeRelay(second);
        manager.PruneExpired(startedUtc + TimeSpan.FromHours(1));

        Assert.False(manager.TryGetRelayTarget(first, "battle", "second", out _));
    }

    [Fact]
    public void ConclusionReportingAfterItsRollbackIsRefusedAndLeavesNoProtection()
    {
        var peer = CreatePeer(1);
        var manager = CreateManager(("late", peer));
        var startedUtc = DateTime.UtcNow;

        Assert.True(manager.TryBeginEmptyInstanceConclusion("battle"));
        manager.PruneExpired(startedUtc + TimeSpan.FromMinutes(6));

        // The rolled-back instance is free again, so the late report is refused and writes no
        // replay protection: a conclusion that misses its deadline has to be claimed again.
        Assert.False(manager.CompleteInstanceConclusion("battle", succeeded: true));
        Assert.Equal(0, manager.GetDiagnostics().ConclusionTombstones);
        Assert.True(manager.TryEnterMission(peer, "late", "battle", out _));
    }

    [Fact]
    public void AbandonedPunchesAndConclusionsStayBoundedOverALongRun()
    {
        var peer = CreatePeer(1);
        var playerManager = new Mock<IPlayerManager>();
        playerManager.Setup(registry => registry.TryGetPeer(It.IsAny<string>(), out peer)).Returns(true);
        var manager = new MissionManager(playerManager.Object);
        var netManager = new NetManager(null);
        var startedUtc = DateTime.UtcNow;

        for (int index = 0; index < 2000; index++)
        {
            manager.HandleIntroductionRequest(
                netManager.NatPunchModule,
                new IPEndPoint(IPAddress.Loopback, 20000 + index),
                new IPEndPoint(IPAddress.Loopback, 40000 + index),
                Authorize(manager, peer, "controller-" + index, "instance-" + index));

            string concluded = "concluded-" + index;
            Assert.True(manager.TryBeginEmptyInstanceConclusion(concluded));
            Assert.True(manager.CompleteInstanceConclusion(concluded, succeeded: true));
        }

        Assert.True(manager.GetDiagnostics().ConclusionTombstones <= 4096);

        manager.PruneExpired(startedUtc + TimeSpan.FromHours(1));

        var counts = manager.GetDiagnostics();
        Assert.Equal(0, counts.ActiveInstances);
        Assert.Equal(0, counts.PunchEndpoints);
        Assert.Equal(0, counts.ConcludingInstances);
        Assert.Equal(0, counts.ConclusionTombstones);
    }

    [Fact]
    public void MaintenanceRunningAgainstLiveTrafficKeepsTheIndexesConsistent()
    {
        var manager = CreateManagerForRandomTraffic(out var peers);
        var random = new Random(20260922);
        var netManager = new NetManager(null);
        var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var maintenance = Task.Run(() =>
        {
            var utcNow = DateTime.UtcNow;
            while (!stop.IsCancellationRequested)
            {
                manager.PruneExpired(utcNow);
                utcNow += TimeSpan.FromSeconds(30);
            }
        });

        var traffic = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                int index = random.Next(peers.Length);
                var peer = peers[index];
                string controllerId = "controller-" + index;
                string instanceId = "instance-" + random.Next(4);

                switch (random.Next(6))
                {
                    case 0:
                        manager.TryEnterMission(peer, controllerId, instanceId, out _);
                        break;
                    case 1:
                        manager.TryLeaveMission(peer, controllerId, instanceId, out _);
                        break;
                    case 2:
                        if (index >= RetiringPeerIndex)
                            manager.HandleDisconnect(peer);
                        break;
                    case 3:
                        manager.TryGetRelayTarget(peer, instanceId, controllerId, out _);
                        break;
                    case 4:
                        if (manager.TryAuthorizeIntroduction(peer, controllerId, instanceId, Guid.NewGuid(), out var token))
                        {
                            manager.HandleIntroductionRequest(
                                netManager.NatPunchModule,
                                new IPEndPoint(IPAddress.Loopback, 56000 + index),
                                new IPEndPoint(IPAddress.Loopback, 57000 + index),
                                token);
                        }
                        break;
                    default:
                        if (manager.TryBeginEmptyInstanceConclusion(instanceId))
                            manager.CompleteInstanceConclusion(instanceId, succeeded: random.Next(2) == 0);
                        break;
                }
            }
        });

        Task.WaitAll(new[] { maintenance, traffic }, TimeSpan.FromSeconds(30));
        manager.PruneExpired(DateTime.UtcNow + TimeSpan.FromHours(1));

        AssertIndexesAreConsistent(manager);
    }

    // Every membership must be reachable from all three indexes, no index may point at an instance
    // the manager has already dropped, and the conclusion collections must agree with them.
    private static void AssertIndexesAreConsistent(MissionManager manager)
    {
        var instances = GetInstances(manager);
        var byPeer = GetMembershipsByPeer(manager);
        var byController = GetMembershipsByController(manager);
        var concluding = GetConclusions(manager, "concludingInstances");
        var concluded = GetConclusions(manager, "concludedInstances");
        var pendingEmpty = GetInstances(manager, "pendingEmptyInstances");

        foreach (var instanceId in pendingEmpty.Keys)
        {
            Assert.Contains(instanceId, concluding.Keys);
            Assert.DoesNotContain(instanceId, instances.Keys);
        }

        foreach (var instanceId in concluding.Keys)
        {
            Assert.DoesNotContain(instanceId, concluded.Keys);
        }

        foreach (var membership in byPeer.Values)
        {
            Assert.True(instances.ContainsKey(membership.Instance.Id));
            Assert.Contains(membership, instances[membership.Instance.Id].Memberships);
            Assert.Same(membership, byController[membership.ControllerId]);
        }

        foreach (var membership in byController.Values)
        {
            Assert.True(instances.ContainsKey(membership.Instance.Id));
            Assert.Same(membership, byPeer[membership.Peer]);
        }

        foreach (var instance in instances.Values)
        {
            Assert.True(instance.Memberships.Count > 0 || instance.PunchEndpoints.Count > 0);
            foreach (var membership in instance.Memberships)
            {
                Assert.Same(membership, byPeer[membership.Peer]);
                Assert.Same(membership, byController[membership.ControllerId]);
            }
        }
    }

    // Peers at or above this index are the only ones the random traffic disconnects, because a
    // disconnected peer is retired for good and could no longer punch.
    private const int RetiringPeerIndex = 12;

    private static MissionManager CreateManagerForRandomTraffic(out NetPeer[] peers)
    {
        var created = new NetPeer[16];
        var playerManager = new Mock<IPlayerManager>();
        for (int index = 0; index < created.Length; index++)
        {
            created[index] = CreatePeer(100 + index);
            var peer = created[index];
            playerManager
                .Setup(manager => manager.TryGetPeer("controller-" + index, out peer))
                .Returns(true);
        }

        peers = created;
        return new MissionManager(playerManager.Object);
    }

    private static void Punch(
        MissionManager manager, NetPeer peer, string controllerId, string instanceId, int port)
    {
        var netManager = new NetManager(null);
        manager.HandleIntroductionRequest(
            netManager.NatPunchModule,
            new IPEndPoint(IPAddress.Loopback, port),
            new IPEndPoint(IPAddress.Loopback, port + 1),
            Authorize(manager, peer, controllerId, instanceId));
    }

    private static Dictionary<NetPeer, MissionMembership> GetMembershipsByPeer(MissionManager manager)
        => (Dictionary<NetPeer, MissionMembership>)typeof(MissionManager)
            .GetField("byPeer", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;

    private static Dictionary<string, MissionMembership> GetMembershipsByController(MissionManager manager)
        => (Dictionary<string, MissionMembership>)typeof(MissionManager)
            .GetField("byController", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;

    private static string Authorize(MissionManager manager, NetPeer peer, string controllerId, string instanceId)
    {
        Assert.True(manager.TryAuthorizeIntroduction(peer, controllerId, instanceId, Guid.NewGuid(), out var token));
        return token;
    }

    private static MissionManager CreateManager(params (string controllerId, NetPeer peer)[] peers)
    {
        var playerManager = new Mock<IPlayerManager>();
        foreach (var (controllerId, peer) in peers)
        {
            var mappedPeer = peer;
            playerManager
                .Setup(manager => manager.TryGetPeer(controllerId, out mappedPeer))
                .Returns(true);
        }

        return new MissionManager(playerManager.Object);
    }

    private static bool HasInstance(MissionManager manager, string instanceId) =>
        GetInstances(manager).ContainsKey(instanceId);

    private static MissionInstance GetInstance(MissionManager manager, string instanceId) =>
        GetInstances(manager)[instanceId];

    private static Dictionary<string, MissionInstance> GetInstances(
        MissionManager manager, string fieldName = "byInstanceId")
    {
        var instanceField = typeof(MissionManager).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Dictionary<string, MissionInstance>)instanceField.GetValue(manager)!;
    }

    private static Dictionary<string, DateTime> GetConclusions(MissionManager manager, string fieldName)
        => (Dictionary<string, DateTime>)typeof(MissionManager)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(manager)!;

    private static NetPeer CreatePeer(int id)
        => (NetPeer)PeerConstructor.Invoke(new object[]
        {
            new NetManager(null),
            new IPEndPoint(IPAddress.Loopback, 52000 + id),
            id,
        });
}
