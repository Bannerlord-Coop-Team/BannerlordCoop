using Common;
using Common.Messaging;
using Common.Network.Data;
using Common.Network.Messages;
using Common.Tests.Utils;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Instances.Handlers;
using Coop.Tests.Mocks;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using LiteNetLib;
using Missions.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using Xunit;

namespace Coop.Tests.Server.Services.Instances;

/// <summary>Exercises crashed settlement-member replacement across NAT and relay membership state.</summary>
public class SettlementMissionReconnectTests
{
    private const string InstanceId = "settlement|tavern";
    private static readonly ConstructorInfo PeerConstructor = typeof(NetPeer).GetConstructor(
        BindingFlags.NonPublic | BindingFlags.Instance,
        binder: null,
        new[] { typeof(NetManager), typeof(IPEndPoint), typeof(int) },
        modifiers: null)!;

    private static NatPunchModule capturedNatPunchModule = null!;
    private static readonly List<(
        IPEndPoint hostInternal,
        IPEndPoint hostExternal,
        IPEndPoint clientInternal,
        IPEndPoint clientExternal,
        string discoveryToken)> CapturedIntroductions = new();

    [Fact]
    public void CrashedMember_ReconnectsOnReplacementPeerWithoutOldNatEndpoint()
    {
        var oldAPeer = CreatePeer(1);
        var bPeer = CreatePeer(2);
        var replacementAPeer = CreatePeer(3);
        var oldAInternal = Endpoint("10.0.0.1", 30001);
        var oldAExternal = Endpoint("198.51.100.1", 40001);
        var bInternal = Endpoint("10.0.0.2", 30002);
        var bExternal = Endpoint("198.51.100.2", 40002);
        var replacementAInternal = Endpoint("10.0.0.3", 30003);
        var replacementAExternal = Endpoint("198.51.100.3", 40003);
        var messageBroker = new TestMessageBroker();
        var network = new TestNetwork();
        var playerManager = new Mock<IPlayerManager>();
        var missionManager = new MissionManager(playerManager.Object);
        RegisterIdentity(playerManager, oldAPeer, "A");
        RegisterIdentity(playerManager, bPeer, "B");
        using var handler = new ServerMissionMembershipHandler(
            messageBroker,
            missionManager,
            network,
            playerManager.Object);
        var natManager = new NetManager(null);
        NatPunchModule natPunchModule = natManager.NatPunchModule;
        var harmony = new Harmony($"coop.tests.settlement-reconnect.{Guid.NewGuid():N}");
        MethodInfo natIntroduce = AccessTools.Method(
            typeof(NatPunchModule),
            nameof(NatPunchModule.NatIntroduce));
        harmony.Patch(
            natIntroduce,
            prefix: new HarmonyMethod(AccessTools.Method(
                typeof(SettlementMissionReconnectTests),
                nameof(CaptureNatIntroduction))));

        capturedNatPunchModule = natPunchModule;
        CapturedIntroductions.Clear();
        try
        {
            Punch(missionManager, natPunchModule, oldAInternal, oldAExternal, oldAPeer, "A");
            messageBroker.Publish(oldAPeer, new NetworkMissionEntered("A", InstanceId));
            Punch(missionManager, natPunchModule, bInternal, bExternal, bPeer, "B");
            messageBroker.Publish(bPeer, new NetworkMissionEntered("B", InstanceId));
            DrainGameThread();

            Assert.True(missionManager.TryGetControllers(InstanceId, out var initialControllers));
            Assert.Equal(new[] { "A", "B" }, initialControllers.OrderBy(id => id));
            Assert.True(missionManager.TryGetRelayTarget(
                bPeer,
                InstanceId,
                "A",
                out NetPeer initialAPeer));
            Assert.Same(oldAPeer, initialAPeer);

            int oldPeerMessageCount = network.SentNetworkMessages[oldAPeer.Id].Count;
            messageBroker.Publish(this, new PlayerDisconnected(oldAPeer, default));

            CapturedIntroductions.Clear();
            RegisterIdentity(playerManager, replacementAPeer, "A");
            Punch(
                missionManager,
                natPunchModule,
                replacementAInternal,
                replacementAExternal,
                replacementAPeer,
                "A");
            DrainGameThread();

            Assert.True(missionManager.TryGetControllers(InstanceId, out var survivingControllers));
            Assert.Equal(new[] { "B" }, survivingControllers);
            Assert.False(missionManager.TryGetRelayTarget(oldAPeer, InstanceId, "B", out _));
            Assert.False(missionManager.TryGetRelayTarget(bPeer, InstanceId, "A", out _));
            Assert.Single(network.GetPeerMessagesFromType<MissionPeerDisconnected>(bPeer));

            var replacementIntroduction = Assert.Single(CapturedIntroductions);
            Assert.Equal(bExternal, replacementIntroduction.hostExternal);
            Assert.Equal(replacementAExternal, replacementIntroduction.clientExternal);
            Assert.True(ConnectionToken.TryParse(replacementIntroduction.discoveryToken, out var discovery));
            Assert.Equal("A", discovery.ControllerId);
            Assert.Equal(InstanceId, discovery.InstanceId);
            Assert.True(replacementIntroduction.discoveryToken.Length <= NatPunchModule.MaxTokenLength);
            Assert.DoesNotContain(
                CapturedIntroductions,
                candidate => candidate.hostExternal.Equals(oldAExternal));

            CapturedIntroductions.Clear();
            Punch(missionManager, natPunchModule, bInternal, bExternal, bPeer, "B");
            var survivorRepunch = Assert.Single(CapturedIntroductions);
            Assert.Equal(replacementAExternal, survivorRepunch.hostExternal);
            Assert.Equal(bExternal, survivorRepunch.clientExternal);

            CapturedIntroductions.Clear();
            messageBroker.Publish(
                replacementAPeer,
                new NetworkMissionEntered("A", InstanceId));
            DrainGameThread();

            Assert.True(missionManager.TryGetControllers(InstanceId, out var reconnectedControllers));
            Assert.Equal(new[] { "A", "B" }, reconnectedControllers.OrderBy(id => id));
            Assert.True(missionManager.TryGetRelayTarget(
                bPeer,
                InstanceId,
                "A",
                out NetPeer currentAPeer));
            Assert.Same(replacementAPeer, currentAPeer);
            Assert.False(missionManager.TryGetRelayTarget(oldAPeer, InstanceId, "B", out _));
            Assert.Equal(oldPeerMessageCount, network.SentNetworkMessages[oldAPeer.Id].Count);
        }
        finally
        {
            capturedNatPunchModule = null!;
            CapturedIntroductions.Clear();
            harmony.Unpatch(natIntroduce, HarmonyPatchType.Prefix, harmony.Id);
        }
    }

    [Fact]
    public void SameInstanceReentry_AuthorizationSurvivesQueuedPreviousLeave()
    {
        var peer = CreatePeer(4);
        var messageBroker = new TestMessageBroker();
        var network = new TestNetwork();
        var playerManager = new Mock<IPlayerManager>();
        RegisterIdentity(playerManager, peer, "A");
        var missionManager = new MissionManager(playerManager.Object);
        using var handler = new ServerMissionMembershipHandler(
            messageBroker, missionManager, network, playerManager.Object);
        var oldRequestId = Guid.NewGuid();
        messageBroker.Publish(peer, new NetworkRequestMissionIntroduction(InstanceId, oldRequestId));
        messageBroker.Publish(peer, new NetworkMissionEntered("A", InstanceId));
        DrainGameThread();
        var oldAuthorization = Assert.Single(
            network.GetPeerMessagesFromType<NetworkMissionIntroductionAuthorized>(peer));

        using var gameThreadBlocked = new ManualResetEventSlim(false);
        using var releaseGameThread = new ManualResetEventSlim(false);
        var newRequestId = Guid.NewGuid();
        try
        {
            GameThread.Run(() =>
            {
                gameThreadBlocked.Set();
                releaseGameThread.Wait();
            });
            Assert.True(gameThreadBlocked.Wait(TimeSpan.FromSeconds(10)));
            messageBroker.Publish(peer, new NetworkMissionLeft("A", InstanceId));
            messageBroker.Publish(peer, new NetworkRequestMissionIntroduction(InstanceId, newRequestId));
            messageBroker.Publish(peer, new NetworkMissionEntered("A", InstanceId));
        }
        finally
        {
            releaseGameThread.Set();
            DrainGameThread();
        }

        var newAuthorization = Assert.Single(
            network.GetPeerMessagesFromType<NetworkMissionIntroductionAuthorized>(peer),
            message => message.RequestId == newRequestId);
        var nat = new NetManager(null).NatPunchModule;
        var endpoint = Endpoint("198.51.100.4", 40004);
        var instances = (Dictionary<string, MissionInstance>)typeof(MissionManager)
            .GetField("byInstanceId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(missionManager)!;
        missionManager.HandleIntroductionRequest(nat, endpoint, endpoint, oldAuthorization.Token);
        Assert.Empty(instances[InstanceId].PunchEndpoints);
        missionManager.HandleIntroductionRequest(nat, endpoint, endpoint, newAuthorization.Token);
        var punch = Assert.Single(instances[InstanceId].PunchEndpoints);
        Assert.Equal(endpoint, punch.External);
        Assert.True(missionManager.TryGetControllers(InstanceId, out var controllers));
        Assert.Equal(new[] { "A" }, controllers);
    }

    private static bool CaptureNatIntroduction(
        NatPunchModule __instance,
        IPEndPoint hostInternal,
        IPEndPoint hostExternal,
        IPEndPoint clientInternal,
        IPEndPoint clientExternal,
        string additionalInfo)
    {
        if (!ReferenceEquals(__instance, capturedNatPunchModule)) return true;
        CapturedIntroductions.Add((
            hostInternal,
            hostExternal,
            clientInternal,
            clientExternal,
            additionalInfo));
        return false;
    }

    private static void RegisterIdentity(
        Mock<IPlayerManager> playerManager,
        NetPeer peer,
        string controllerId)
    {
        var player = new Player(
            controllerId,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
        var mappedPlayer = player;
        var mappedPeer = peer;
        playerManager
            .Setup(manager => manager.TryGetPlayer(peer, out mappedPlayer))
            .Returns(true);
        playerManager
            .Setup(manager => manager.TryGetPeer(controllerId, out mappedPeer))
            .Returns(true);
    }

    private static void Punch(
        MissionManager missionManager,
        NatPunchModule natPunchModule,
        IPEndPoint internalEndpoint,
        IPEndPoint externalEndpoint,
        NetPeer peer,
        string controllerId)
    {
        Assert.True(missionManager.TryAuthorizeIntroduction(
            peer, controllerId, InstanceId, Guid.NewGuid(), out var token));
        missionManager.HandleIntroductionRequest(
            natPunchModule,
            internalEndpoint,
            externalEndpoint,
            token);
    }

    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);

    private static IPEndPoint Endpoint(string address, int port) =>
        new(IPAddress.Parse(address), port);

    private static NetPeer CreatePeer(int id) =>
        (NetPeer)PeerConstructor.Invoke(new object[]
        {
            new NetManager(null),
            new IPEndPoint(IPAddress.Loopback, 52000 + id),
            id,
        });
}
