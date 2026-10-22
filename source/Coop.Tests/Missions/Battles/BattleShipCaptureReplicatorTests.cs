using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Agents.Handlers;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>In-mission ship capture: the captured hull's authority decides, and every client replays the swap in place.</summary>
public class BattleShipCaptureReplicatorTests
{
    private const string Own = "local";
    private const string Host = "host";
    private const string Captor = "captor";
    private const string Observer = "observer";

    // Each check names its own reason, in the order the authority reads them.
    [Theory]
    [InlineData(0, "not_authority")]
    [InlineData(1, "already_captured")]
    [InlineData(2, "captor_not_in_mission")]
    [InlineData(3, "captor_hull_invalid")]
    [InlineData(4, "not_enemy")]
    [InlineData(5, "busy")]
    [InlineData(6, "cannot_be_taken_over")]
    [InlineData(7, "sinking")]
    [InlineData(8, "not_vacant")]
    [InlineData(-1, null)]
    public void Decide_NamesTheFirstFailingCheck(int failing, string? expected)
    {
        var facts = new CaptureFacts(isAuthority: failing != 0, alreadyCaptured: failing == 1, captorInMission: failing != 2,
            captorHullValid: failing != 3, isEnemy: failing != 4, busy: failing == 5, canBeTakenOver: failing != 6,
            isSinking: failing == 7, isVacant: failing != 8);

        Assert.Equal(expected, BattleShipCaptureReplicator.Decide(facts));
    }

    // Only the prize's current authority acts on a request; nobody else sends anything.
    [Fact]
    public void Request_IsIgnoredByAClientThatDoesNotSimulateThePrize()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);

        harness.Captures.AcceptRequest(new NetworkShipCaptureRequest(prize.ShipId, Captor, captorShip.ShipId, 1));

        harness.Network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        harness.Engine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Never);
        Assert.Equal(1L, (long)harness.Inspect()["requestsIgnored"]!);
    }

    // The authority applies the capture itself, broadcasts it, and hands the prize to its captor in place.
    [Fact]
    public void Request_ToTheAuthorityOfAVacantEnemyHull_AppliesAndBroadcastsTheCapture()
    {
        var harness = new Harness();
        var prize = harness.Register(Own, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        harness.Vacant.Add(prize.Hull);

        harness.Captures.AcceptRequest(new NetworkShipCaptureRequest(prize.ShipId, Captor, captorShip.ShipId, 1));

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCaptured
            && ((NetworkShipCaptured)m).ShipId == prize.ShipId && ((NetworkShipCaptured)m).PreviousAuthority == Own)), Times.Once);
        harness.Engine.Verify(e => e.ApplyCapture(prize.Hull, captorShip.Hull), Times.Once);
        harness.Engine.Verify(e => e.ReleaseHull(prize.Hull), Times.Once);
        Assert.Equal(Captor, prize.CurrentAuthority);
        Assert.Equal(Captor, prize.CaptorControllerId);
        Assert.False(prize.FollowsBattleHost);
        Assert.True(captorShip.IsOrphaned);
    }

    // The crew still holds the hull, so the authority refuses and tells the captor why.
    [Fact]
    public void Request_ForAHullItsCrewStillHolds_IsRefusedToTheCaptor()
    {
        var harness = new Harness();
        var prize = harness.Register(Own, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);

        harness.Captures.AcceptRequest(new NetworkShipCaptureRequest(prize.ShipId, Captor, captorShip.ShipId, 1));

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCaptureRejected
            && ((NetworkShipCaptureRejected)m).Reason == "not_vacant" && ((NetworkShipCaptureRejected)m).CaptorControllerId == Captor)),
            Times.Once);
        Assert.Equal(Own, prize.CurrentAuthority);
        Assert.Null(prize.CaptorControllerId);
    }

    // Q6: a sinking hull is refused even when vacant.
    [Fact]
    public void Request_ForASinkingHull_IsRefused()
    {
        var harness = new Harness();
        var prize = harness.Register(Own, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        harness.Vacant.Add(prize.Hull);
        harness.Engine.Setup(e => e.IsSinking(prize.Hull)).Returns(true);

        harness.Captures.AcceptRequest(new NetworkShipCaptureRequest(prize.ShipId, Captor, captorShip.ShipId, 1));

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCaptureRejected
            && ((NetworkShipCaptureRejected)m).Reason == "sinking")), Times.Once);
    }

    // 2.2 case 3: the battle host's own capture of an AI hull it simulates is decided at once, moves no authority, and ends the
    // hull's host following; it still goes out so every client replays the swap.
    [Fact]
    public void TimerElapsed_OnAnAiHullThisHostSimulates_CapturesAtOnce_WithoutMovingItsAuthority()
    {
        var harness = new Harness();
        var prize = harness.Register(Own, BattleSideEnum.Defender, npc: true);
        var ownHull = harness.Register(Own, BattleSideEnum.Attacker);
        harness.Vacant.Add(prize.Hull);
        var main = Uninitialized<Agent>();
        harness.Engine.Setup(e => e.CrewHullOf(main)).Returns(ownHull.Hull);

        harness.Broker.Publish(this, new ShipCaptureTimerElapsed(prize.Hull, main));

        Assert.Equal(Own, prize.CurrentAuthority);
        Assert.False(prize.FollowsBattleHost);
        harness.Engine.Verify(e => e.ApplyCapture(prize.Hull, ownHull.Hull), Times.Once);
        harness.Engine.Verify(e => e.AdoptHull(It.IsAny<MissionObject>()), Times.Never);
        harness.Engine.Verify(e => e.ReleaseHull(It.IsAny<MissionObject>()), Times.Never);
        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCaptured)), Times.Once);
        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCaptureRequest)), Times.Never);
    }

    // A captor asks the authority once per prize; a second elapsed timer while it waits sends nothing.
    [Fact]
    public void TimerElapsed_OnACopy_AsksItsAuthorityOnce()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var ownHull = harness.Register(Own, BattleSideEnum.Attacker);
        var main = Uninitialized<Agent>();
        harness.Engine.Setup(e => e.CrewHullOf(main)).Returns(ownHull.Hull);

        harness.Broker.Publish(this, new ShipCaptureTimerElapsed(prize.Hull, main));
        harness.Broker.Publish(this, new ShipCaptureTimerElapsed(prize.Hull, main));

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCaptureRequest
            && ((NetworkShipCaptureRequest)m).CaptorShipId == ownHull.ShipId)), Times.Once);
        Assert.NotNull(harness.Captures.PendingRequestAge(prize.ShipId));
    }

    // Q7: no answer within the timeout (the authority left before it decided) tells the player, who can try again.
    [Fact]
    public void PendingRequest_WithNoAnswer_ExpiresAndTellsThePlayer()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var ownHull = harness.Register(Own, BattleSideEnum.Attacker);
        harness.Captures.RequestCapture(prize, ownHull);

        harness.Captures.Tick(BattleShipCaptureReplicator.RequestTimeoutSeconds - 0.5f);
        int toldBefore = harness.Told.Count;
        harness.Captures.Tick(1f);

        Assert.Equal(0, toldBefore);
        Assert.Contains("nobody answered", Assert.Single(harness.Told));
        Assert.Null(harness.Captures.PendingRequestAge(prize.ShipId));
        Assert.Equal(1L, (long)harness.Inspect()["captureExpired"]!);
    }

    // Q7: a refusal reaches the player once, for the request still waiting.
    [Fact]
    public void Rejection_OfAWaitingRequest_TellsThePlayerOnce()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var ownHull = harness.Register(Own, BattleSideEnum.Attacker);
        harness.Captures.RequestCapture(prize, ownHull);

        harness.Captures.AcceptRejection(new NetworkShipCaptureRejected(prize.ShipId, Own, "not_vacant"));
        harness.Captures.AcceptRejection(new NetworkShipCaptureRejected(prize.ShipId, Own, "already_captured"));

        Assert.Contains("crew still holds it", Assert.Single(harness.Told));
        Assert.Null(harness.Captures.PendingRequestAge(prize.ShipId));
    }

    // Every client replays a confirm from the prize's authority: the captor adopts its copy in place, others keep a copy.
    [Fact]
    public void Confirm_OnTheCaptor_AdoptsItsCopyInPlace()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var ownHull = harness.Register(Own, BattleSideEnum.Attacker);

        harness.Captures.AcceptConfirm(new NetworkShipCaptured(prize.ShipId, Own, ownHull.ShipId, Host, 1));

        harness.Engine.Verify(e => e.AdoptHull(prize.Hull), Times.Once);
        harness.Engine.Verify(e => e.ApplyCapture(prize.Hull, ownHull.Hull), Times.Once);
        harness.Engine.Verify(e => e.ReplaceHull(It.IsAny<MissionObject>()), Times.Never);
        Assert.Equal(Own, prize.CurrentAuthority);
        Assert.True(ownHull.IsOrphaned);
        Assert.Equal(BattleSideEnum.Attacker, ownHull.OrphanedSide);
    }

    // 2.3 step 5: only the captor tells the server, on the relay its fates use, so the server takes the prize's fates from it.
    [Fact]
    public void Confirm_OnlyTheCaptorReportsTheCaptureToTheServer()
    {
        var captorSide = new Harness();
        var prize = captorSide.Register(Host, BattleSideEnum.Defender, npc: true);
        var ownHull = captorSide.Register(Own, BattleSideEnum.Attacker);
        captorSide.Engine.Setup(e => e.CampaignShipIdOf(prize.Hull)).Returns("Ship_7");
        var observerSide = new Harness();
        var observed = observerSide.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorCopy = observerSide.Register(Captor, BattleSideEnum.Attacker);
        observerSide.Engine.Setup(e => e.CampaignShipIdOf(observed.Hull)).Returns("Ship_7");

        captorSide.Captures.AcceptConfirm(new NetworkShipCaptured(prize.ShipId, Own, ownHull.ShipId, Host, 1));
        observerSide.Captures.AcceptConfirm(new NetworkShipCaptured(observed.ShipId, Captor, captorCopy.ShipId, Host, 1));

        captorSide.Relay.Verify(r => r.SendAll(It.Is<IMessage>(m => m is NetworkBattleShipCaptured
            && ((NetworkBattleShipCaptured)m).CampaignShipId == "Ship_7" && ((NetworkBattleShipCaptured)m).InstanceId == "MapEvent_1")),
            Times.Once);
        observerSide.Relay.Verify(r => r.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    // 2.3 step 1: a client that never simulated the prize can't confirm its capture.
    [Fact]
    public void Confirm_FromAClientThatNeverSimulatedThePrize_IsDropped()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);

        harness.Captures.AcceptConfirm(new NetworkShipCaptured(prize.ShipId, Captor, captorShip.ShipId, Observer, 1));

        harness.Engine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Never);
        Assert.Equal(Host, prize.CurrentAuthority);
        Assert.Equal("not_authority", (string?)harness.Inspect()["lastDrop"]);
    }

    // 2.6: the old host's confirm arriving after a migration moved the prize to its successor, and after the old host left,
    // still applies, so every client ends with the same state whichever event came first.
    [Fact]
    public void Confirm_FromTheHostBeforeAMigrationAndItsDeparture_StillApplies()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        prize.CurrentAuthority = Observer;
        harness.Members.Remove(Host);

        harness.Captures.AcceptConfirm(new NetworkShipCaptured(prize.ShipId, Captor, captorShip.ShipId, Host, 1));

        harness.Engine.Verify(e => e.ApplyCapture(prize.Hull, captorShip.Hull), Times.Once);
        Assert.Equal(Captor, prize.CurrentAuthority);
        Assert.Equal(Captor, prize.CaptorControllerId);
    }

    // A successor confirming what the old host already confirmed changes nothing.
    [Fact]
    public void Confirm_Repeated_AppliesOnce()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        var confirm = new NetworkShipCaptured(prize.ShipId, Captor, captorShip.ShipId, Host, 1);

        harness.Captures.AcceptConfirm(confirm);
        harness.Captures.AcceptConfirm(new NetworkShipCaptured(prize.ShipId, Captor, captorShip.ShipId, Captor, 2));

        harness.Engine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Once);
        Assert.Equal(1L, (long)harness.Inspect()["duplicateConfirms"]!);
        Assert.Equal(1, harness.Ships.DepartedHulls(BattleSideEnum.Defender));
    }

    // 2.6 late join: a confirm for a hull whose record still waits here applies once the hull spawned.
    [Fact]
    public void Confirm_ForAHullNotRegisteredYet_WaitsForIt()
    {
        var harness = new Harness();
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        var prizeId = Guid.NewGuid();
        harness.Registry.MarkAwaiting(prizeId);

        harness.Captures.AcceptConfirm(new NetworkShipCaptured(prizeId, Captor, captorShip.ShipId, Host, 1));
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true, prizeId);
        harness.Captures.Tick(0.1f);

        harness.Engine.Verify(e => e.ApplyCapture(prize.Hull, captorShip.Hull), Times.Once);
        Assert.Equal(Captor, prize.CaptorControllerId);
    }

    // A mid-swap prize (a successor taking over its AI hulls) takes the capture after the swap's last phase.
    [Fact]
    public void Confirm_ForAHullMidSwap_WaitsForTheSwap()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        prize.CurrentAuthority = Own;
        harness.HullAuthority.Replace(prize.ShipId);

        harness.Captures.AcceptConfirm(new NetworkShipCaptured(prize.ShipId, Captor, captorShip.ShipId, Host, 1));
        bool appliedMidSwap = prize.CaptorControllerId != null;
        harness.HullAuthority.Dispose();
        harness.Captures.Tick(0.1f);

        Assert.False(appliedMidSwap);
        Assert.Equal(Captor, prize.CaptorControllerId);
    }

    // A rival's request reaching the old authority after it gave the prize away gets an answer instead of a timeout.
    [Fact]
    public void Request_FromARivalAfterTheAuthorityConfirmedAnother_IsRefusedAsAlreadyCaptured()
    {
        var harness = new Harness();
        var prize = harness.Register(Own, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        var rivalShip = harness.Register(Observer, BattleSideEnum.Attacker);
        harness.Vacant.Add(prize.Hull);

        harness.Captures.AcceptRequest(new NetworkShipCaptureRequest(prize.ShipId, Captor, captorShip.ShipId, 1));
        harness.Captures.AcceptRequest(new NetworkShipCaptureRequest(prize.ShipId, Observer, rivalShip.ShipId, 1));

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCaptured)), Times.Once);
        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCaptureRejected
            && ((NetworkShipCaptureRejected)m).CaptorControllerId == Observer
            && ((NetworkShipCaptureRejected)m).Reason == "already_captured")), Times.Once);
    }

    // Q5: capturing the last enemy hull counts it gone from its side, so vanilla's retreat rule ends the battle there.
    [Fact]
    public void Capture_CountsThePrizeGoneFromItsOldSide()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);

        harness.Captures.AcceptConfirm(new NetworkShipCaptured(prize.ShipId, Captor, captorShip.ShipId, Host, 1));

        Assert.Equal(1, harness.Ships.DepartedHulls(BattleSideEnum.Defender));
        Assert.Equal(0, harness.Ships.DepartedHulls(BattleSideEnum.Attacker));
#if DEBUG
        Assert.True(global::Missions.Naval.CoopNavalBattleEndLogic.KeepsRetreat(harness.Ships.DepartedHulls(BattleSideEnum.Defender),
            hullsLeft: 0));
#endif
    }

    // 2.6: a capture that arrives after its captor left here removes the prize too, as it left with the captor elsewhere.
    [Fact]
    public void Confirm_AfterTheCaptorLeftHere_RemovesThePrize()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShipId = Guid.NewGuid();
        harness.Registry.MarkRemoved(captorShipId);
        harness.Members.Remove(Captor);

        harness.Captures.AcceptConfirm(new NetworkShipCaptured(prize.ShipId, Captor, captorShipId, Host, 1));

        Assert.False(harness.Registry.TryGet(prize.ShipId, out _));
        harness.Engine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Never);
    }

    private static T Uninitialized<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    // The real ship replicator, hull authority and registry over a mocked engine, with the capture replicator under test.
    private sealed class Harness
    {
        public Mock<IBattleNetwork> Network { get; } = new Mock<IBattleNetwork>();
        public Mock<INetwork> Relay { get; } = new Mock<INetwork>();
        public Mock<INavalShipEngine> Engine { get; } = new Mock<INavalShipEngine>();
        public MessageBroker Broker { get; } = new MessageBroker();
        public NetworkShipRegistry Registry { get; } = new NetworkShipRegistry();
        public List<string> Members { get; } = new List<string> { Host, Captor, Observer };
        public HashSet<MissionObject> Vacant { get; } = new HashSet<MissionObject>();
        public List<string> Told { get; } = new List<string>();
        public List<MissionObject> LiveHulls { get; } = new List<MissionObject>();
        public NavalHullAuthority HullAuthority { get; }
        public BattleShipReplicator Ships { get; }
        public BattleShipCaptureReplicator Captures { get; }
        private readonly Dictionary<MissionObject, BattleSideEnum> sides = new Dictionary<MissionObject, BattleSideEnum>();

        public Harness()
        {
            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(c => c.ShipRegistry).Returns(Registry);
            component.SetupGet(c => c.AgentMovementHandler).Returns(Mock.Of<IAgentMovementHandler>());

            var session = new Mock<IBattleSession>();
            session.SetupGet(s => s.OwnControllerId).Returns(Own);
            session.SetupGet(s => s.HostEpoch).Returns(1);
            session.SetupGet(s => s.InstanceId).Returns("MapEvent_1");
            session.Setup(s => s.IsOwn(It.IsAny<string>())).Returns((string id) => id == Own);
            session.SetupGet(s => s.IsNaval).Returns(true);
            session.SetupGet(s => s.HasInstance).Returns(true);

            Engine.SetupGet(e => e.Hulls).Returns(() => LiveHulls);
            Engine.Setup(e => e.AgentsOnDeck(It.IsAny<MissionObject>())).Returns(Array.Empty<Agent>());
            Engine.Setup(e => e.ParkHullAgents(It.IsAny<MissionObject>())).Returns(Array.Empty<HullSwapAgent>());
            Engine.Setup(e => e.SideOf(It.IsAny<MissionObject>())).Returns((MissionObject hull) =>
                sides.TryGetValue(hull, out var side) ? side : BattleSideEnum.None);
            Engine.Setup(e => e.IsVacantForCapture(It.IsAny<MissionObject>())).Returns((MissionObject hull) => Vacant.Contains(hull));
            Engine.Setup(e => e.CanBeTakenOver(It.IsAny<MissionObject>())).Returns(true);
            Engine.Setup(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()))
                .Callback((MissionObject prize, MissionObject captor) =>
                {
                    sides[prize] = sides[captor];
                    sides[captor] = BattleSideEnum.None;
                });

            var members = new Mock<IMissionContext>();
            members.SetupGet(m => m.ControllersInMission).Returns(() => Members.ToArray());
            var epochs = new HostEpochPolicy();
            var context = new BattleContext(session.Object, Mock.Of<IBattleDeploymentCoordinator>(), component.Object, epochs,
                Mock.Of<IBattleResultCommitter>(), Mock.Of<IReinforcementFielder>(), members.Object, Mock.Of<IBattleTroopWithdrawal>(),
                Mock.Of<IBattlePartyWithdrawal>());
            HullAuthority = new NavalHullAuthority(session.Object, component.Object, Engine.Object, context.TroopWithdrawal);
            Ships = new BattleShipReplicator(Network.Object, Broker, context, Engine.Object, Mock.Of<IBattleTeamResolver>(),
                Mock.Of<IObjectManager>(), HullAuthority,
                new BattleShipConditionStream(Network.Object, session.Object, Engine.Object, epochs, members.Object),
                Mock.Of<IAgentStationUseReplicator>());
            Captures = new BattleShipCaptureReplicator(Network.Object, Relay.Object, Broker, context, Engine.Object, Ships, HullAuthority);
            Broker.Subscribe<SendInformationMessage>(Handle_Told);
        }

        // Keeps the told lines; an instance method, so the broker's weak delegate stays alive with the harness.
        private void Handle_Told(MessagePayload<SendInformationMessage> payload) => Told.Add(payload.What.Text);

        public NetworkShipInfo Register(string authority, BattleSideEnum side, bool npc = false, Guid? shipId = null)
        {
            var hull = ShipTestHulls.Create();
            sides[hull] = side;
            LiveHulls.Add(hull);
            var ship = new NetworkShipInfo(shipId ?? Guid.NewGuid(), authority, "MapEventParty_" + authority, npc, hull,
                Uninitialized<Formation>());
            Assert.True(Registry.TryRegister(ship));
            return ship;
        }

        public JObject Inspect() => JObject.FromObject(Captures.Inspect());
    }
}
