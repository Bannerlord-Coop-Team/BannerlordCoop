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

/// <summary>
/// In-mission ship capture: the captured hull's authority decides (<see cref="BattleShipCaptureReplicator"/>), and every client
/// applies the capture in its hulls' capture order (<see cref="ShipCaptureApplier"/>).
/// </summary>
public class BattleShipCaptureReplicatorTests
{
    private const string Own = "local";
    private const string Host = "host";
    private const string Successor = "successor";
    private const string Captor = "captor";
    private const string Observer = "observer";

    // Each check names its own reason, in the order the authority reads them.
    [Theory]
    [InlineData(0, ShipCaptureRefusal.NotAuthority)]
    [InlineData(1, ShipCaptureRefusal.AlreadyCaptured)]
    [InlineData(2, ShipCaptureRefusal.CaptorNotInMission)]
    [InlineData(3, ShipCaptureRefusal.CaptorHullInvalid)]
    [InlineData(4, ShipCaptureRefusal.NotEnemy)]
    [InlineData(5, ShipCaptureRefusal.Busy)]
    [InlineData(6, ShipCaptureRefusal.CannotBeTakenOver)]
    [InlineData(7, ShipCaptureRefusal.Sinking)]
    [InlineData(8, ShipCaptureRefusal.NotVacant)]
    [InlineData(9, ShipCaptureRefusal.SwapUnavailable)]
    [InlineData(-1, null)]
    public void Decide_NamesTheFirstFailingCheck(int failing, ShipCaptureRefusal? expected)
    {
        var facts = new CaptureFacts(isAuthority: failing != 0, alreadyCaptured: failing == 1, captorInMission: failing != 2,
            captorHullValid: failing != 3, isEnemy: failing != 4, busy: failing == 5, canBeTakenOver: failing != 6,
            isSinking: failing == 7, isVacant: failing != 8, swapReady: failing != 9);

        Assert.Equal(expected, BattleShipCaptureReplicator.Decide(facts));
    }

    // A4: a request reaching a client that doesn't simulate the prize (any more) gets the captor an answer, and only the captor.
    [Fact]
    public void Request_ToAClientThatDoesNotSimulateThePrize_IsRefusedToTheCaptorAlone()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);

        harness.Exchange.AcceptRequest(new NetworkShipCaptureRequest(prize.ShipId, Captor, captorShip.ShipId));

        harness.Network.Verify(n => n.Send(Captor, It.Is<IMessage>(m => m is NetworkShipCaptureRejected
            && ((NetworkShipCaptureRejected)m).Reason == ShipCaptureRefusal.NotAuthority)), Times.Once);
        harness.Network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        harness.CaptureEngine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Never);
    }

    // The authority applies the capture itself, broadcasts it numbered by both hulls' capture counts, and hands the prize to its
    // captor in place.
    [Fact]
    public void Request_ToTheAuthorityOfAVacantEnemyHull_AppliesAndBroadcastsTheCapture()
    {
        var harness = new Harness();
        var prize = harness.Register(Own, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        harness.Vacant.Add(prize.Hull);

        harness.Exchange.AcceptRequest(new NetworkShipCaptureRequest(prize.ShipId, Captor, captorShip.ShipId));

        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCaptured
            && ((NetworkShipCaptured)m).ShipId == prize.ShipId && ((NetworkShipCaptured)m).PreviousAuthority == Own
            && ((NetworkShipCaptured)m).PrizeSequence == 0 && ((NetworkShipCaptured)m).CaptorSequence == 0
            && ((NetworkShipCaptured)m).HostEpoch == 1)), Times.Once);
        harness.CaptureEngine.Verify(e => e.ApplyCapture(prize.Hull, captorShip.Hull), Times.Once);
        harness.Engine.Verify(e => e.ReleaseHull(prize.Hull), Times.Once);
        Assert.Equal(Captor, prize.CurrentAuthority);
        Assert.Equal(Captor, prize.CaptorControllerId);
        Assert.Equal(captorShip.ShipId, prize.Capture!.Value.CaptorShipId);
        Assert.Equal(BattleSideEnum.Defender, prize.Capture!.Value.FromSide);
        Assert.False(prize.FollowsBattleHost);
        Assert.True(captorShip.IsOrphaned);
        Assert.Equal(1, prize.CaptureSequence);
        Assert.Equal(1, captorShip.CaptureSequence);
    }
    // R48 L6 review: a request that arrives over the mesh waits in the arrivals queue, which the naval tick applies before the
    // async agent tick starts, never while the game-thread queue drains alongside it.
    // starts, never while the game-thread queue drains alongside it.
    [Fact]
    public void ArrivedRequest_IsDecidedWhenTheArrivalsApply()
    {
        var harness = new Harness();
        var prize = harness.Register(Own, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        harness.Vacant.Add(prize.Hull);

        harness.Exchange.QueueRequest(new NetworkShipCaptureRequest(prize.ShipId, Captor, captorShip.ShipId));

        harness.CaptureEngine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Never);
        harness.Arrivals.ApplyAll();
        harness.CaptureEngine.Verify(e => e.ApplyCapture(prize.Hull, captorShip.Hull), Times.Once);
        Assert.Equal(Captor, prize.CurrentAuthority);
    }

    // R48 L6 review: an arrived confirm applies when the arrivals apply, and one held until its captor's hull registered applies
    // from the next tick's retry.
    [Fact]
    public void ArrivedAndHeldConfirms_ApplyFromTheTick()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        var heldPrize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var lateCaptorId = Guid.NewGuid();

        harness.Applier.QueueConfirm(Confirm(prize, Captor, captorShip, Host));
        harness.Applier.QueueConfirm(new NetworkShipCaptured(heldPrize.ShipId, Captor, lateCaptorId, Host, 1, 0, 0));
        harness.CaptureEngine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Never);

        Assert.Equal(2L, (long)harness.Inspect()["arrivalsWaiting"]!);
        harness.Arrivals.ApplyAll();
        harness.CaptureEngine.Verify(e => e.ApplyCapture(prize.Hull, captorShip.Hull), Times.Once);
        Assert.Single((JArray)harness.Inspect()["applied"]!["heldConfirms"]!);

        var lateCaptor = harness.Register(Captor, BattleSideEnum.Attacker, shipId: lateCaptorId);
        harness.Applier.Tick(0.1f);

        harness.CaptureEngine.Verify(e => e.ApplyCapture(heldPrize.Hull, lateCaptor.Hull), Times.Once);
        Assert.Empty((JArray)harness.Inspect()["applied"]!["heldConfirms"]!);
        Assert.Equal(0L, (long)harness.Inspect()["arrivalsWaiting"]!);
    }

    // R46: a swap vanilla can't run on the authority's hulls is refused before anything changes, so no client half-applies it.
    [Fact]
    public void Request_WhoseSwapCannotRunAtTheAuthority_IsRefusedBeforeAnythingChanges()
    {
        var harness = new Harness();
        var prize = harness.Register(Own, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        harness.Vacant.Add(prize.Hull);
        harness.CaptureEngine.Setup(e => e.CheckCapture(prize.Hull, captorShip.Hull)).Returns("captor_hull_has_no_formation");

        harness.Exchange.AcceptRequest(new NetworkShipCaptureRequest(prize.ShipId, Captor, captorShip.ShipId));

        harness.Network.Verify(n => n.Send(Captor, It.Is<IMessage>(m => m is NetworkShipCaptureRejected
            && ((NetworkShipCaptureRejected)m).Reason == ShipCaptureRefusal.SwapUnavailable)), Times.Once);
        harness.Network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        harness.CaptureEngine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Never);
        harness.Engine.Verify(e => e.ReleaseHull(It.IsAny<MissionObject>()), Times.Never);
        Assert.Equal(Own, prize.CurrentAuthority);
        Assert.Equal(0, prize.CaptureSequence);
        Assert.False(captorShip.IsOrphaned);
    }

    // R46 review: a receiver whose swap can't run yet holds the confirm without changing anything, and applies it whole once
    // the swap can run; the hold is logged once, not per retry.
    [Fact]
    public void Confirm_WhoseSwapCannotRunYet_IsHeldUnchanged_AndAppliesOnceItCan()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        string blocker = "captor_hull_has_no_formation";
        harness.CaptureEngine.Setup(e => e.CheckCapture(prize.Hull, captorShip.Hull)).Returns(() => blocker);

        harness.Applier.AcceptConfirm(Confirm(prize, Captor, captorShip, Host));
        harness.Applier.Tick(1f);
        harness.Applier.Tick(1f);

        harness.CaptureEngine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Never);
        harness.Engine.Verify(e => e.ReleaseHull(It.IsAny<MissionObject>()), Times.Never);
        Assert.Equal(Host, prize.CurrentAuthority);
        Assert.Equal(0, prize.CaptureSequence);
        Assert.False(captorShip.IsOrphaned);
        var held = (JObject)harness.Inspect()["applied"]!;
        Assert.Equal(1L, (long)held["swapsBlocked"]!);
        Assert.Single((JArray)held["heldConfirms"]!);

        blocker = null;
        harness.Applier.Tick(1f);

        harness.CaptureEngine.Verify(e => e.ApplyCapture(prize.Hull, captorShip.Hull), Times.Once);
        Assert.Equal(Captor, prize.CaptorControllerId);
        Assert.Equal(1, prize.CaptureSequence);
        Assert.True(captorShip.IsOrphaned);
        Assert.Empty((JArray)harness.Inspect()["applied"]!["heldConfirms"]!);
    }

    // R46 review: another client's capture this one still can't replay after the wait is given up, loudly; nothing changed.
    [Fact]
    public void Confirm_WhoseSwapNeverRunsHere_IsGivenUpAfterTheWait()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        harness.CaptureEngine.Setup(e => e.CheckCapture(prize.Hull, captorShip.Hull)).Returns("hull_removed");

        harness.Applier.AcceptConfirm(Confirm(prize, Captor, captorShip, Host));
        harness.Applier.Tick(ShipCaptureApplier.SwapWaitSeconds - 1f);
        bool heldBeforeTheWait = ((JArray)harness.Inspect()["applied"]!["heldConfirms"]!).Count == 1;
        harness.Applier.Tick(2f);

        Assert.True(heldBeforeTheWait);
        harness.CaptureEngine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Never);
        Assert.Equal(Host, prize.CurrentAuthority);
        Assert.Null(prize.CaptorControllerId);
        var applied = (JObject)harness.Inspect()["applied"]!;
        Assert.Empty((JArray)applied["heldConfirms"]!);
        Assert.Equal(1L, (long)applied["swapsGivenUp"]!);
        Assert.Equal("swap_blocked:hull_removed", (string)applied["lastDrop"]!);
    }

    // R46 review: every other client gave the prize to its captor, so a captor whose swap never runs still takes the hull over
    // (authority, registry, records) without vanilla's swap once the wait is out, instead of leaving it simulated by nobody.
    [Fact]
    public void Confirm_ToACaptorWhoseSwapNeverRuns_TakesTheHullOverWithoutTheSwap_AfterTheWait()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Own, BattleSideEnum.Attacker);
        harness.CaptureEngine.Setup(e => e.CheckCapture(prize.Hull, captorShip.Hull)).Returns("captor_formation_holds_another_hull");

        harness.Applier.AcceptConfirm(Confirm(prize, Own, captorShip, Host));
        harness.Applier.Tick(ShipCaptureApplier.SwapWaitSeconds - 1f);
        string authorityDuringTheWait = prize.CurrentAuthority;
        harness.Applier.Tick(2f);

        Assert.Equal(Host, authorityDuringTheWait);
        Assert.Equal(Own, prize.CurrentAuthority);
        Assert.Equal(Own, prize.CaptorControllerId);
        Assert.Equal(1, prize.CaptureSequence);
        Assert.True(captorShip.IsOrphaned);
        harness.Engine.Verify(e => e.AdoptHull(prize.Hull), Times.Once);
        harness.CaptureEngine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Never);
        Assert.Equal(1L, (long)harness.Inspect()["applied"]!["capturesWithoutSwap"]!);
    }

    [Theory]
    [InlineData(0f, 29.9f, false)]
    [InlineData(0f, 30f, true)]
    [InlineData(5f, 40f, true)]
    public void SwapWaitExpired_AfterTheBoundedWait(float since, float now, bool expected)
    {
        Assert.Equal(expected, ShipCaptureApplier.SwapWaitExpired(since, now));
    }

    [Fact]
    public void DescribeReason_NamesAnUnavailableSwap()
    {
        Assert.Contains("can't change hands", BattleShipCaptureReplicator.DescribeReason(ShipCaptureRefusal.SwapUnavailable));
    }

    // The crew still holds the hull, so the authority refuses and tells the captor why.
    [Fact]
    public void Request_ForAHullItsCrewStillHolds_IsRefusedToTheCaptor()
    {
        var harness = new Harness();
        var prize = harness.Register(Own, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);

        harness.Exchange.AcceptRequest(new NetworkShipCaptureRequest(prize.ShipId, Captor, captorShip.ShipId));

        harness.Network.Verify(n => n.Send(Captor, It.Is<IMessage>(m => m is NetworkShipCaptureRejected
            && ((NetworkShipCaptureRejected)m).Reason == ShipCaptureRefusal.NotVacant)), Times.Once);
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

        harness.Exchange.AcceptRequest(new NetworkShipCaptureRequest(prize.ShipId, Captor, captorShip.ShipId));

        harness.Network.Verify(n => n.Send(Captor, It.Is<IMessage>(m => m is NetworkShipCaptureRejected
            && ((NetworkShipCaptureRejected)m).Reason == ShipCaptureRefusal.Sinking)), Times.Once);
    }

    // C5: a promoted host refuses an inherited AI hull for the settle window, so a capture the old host confirmed just before it
    // left reaches it first and no two deciders write one place in the hull's capture order; afterwards it decides.
    [Fact]
    public void Request_ToAJustPromotedHost_IsBusyForItsInheritedAiHulls_UntilTheSettleWindowPassed()
    {
        var harness = new Harness(host: Host);
        var prize = harness.Register(Own, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        harness.Vacant.Add(prize.Hull);
        harness.Exchange.Tick(0.1f);
        harness.Promote(epoch: 2);

        harness.Exchange.AcceptRequest(new NetworkShipCaptureRequest(prize.ShipId, Captor, captorShip.ShipId));
        bool decidedInsideTheWindow = prize.CaptorControllerId != null;
        harness.Exchange.Tick(BattleShipCaptureReplicator.PromotionSettleSeconds + 0.1f);
        harness.Exchange.AcceptRequest(new NetworkShipCaptureRequest(prize.ShipId, Captor, captorShip.ShipId));

        Assert.False(decidedInsideTheWindow);
        harness.Network.Verify(n => n.Send(Captor, It.Is<IMessage>(m => m is NetworkShipCaptureRejected
            && ((NetworkShipCaptureRejected)m).Reason == ShipCaptureRefusal.Busy)), Times.Once);
        Assert.Equal(Captor, prize.CaptorControllerId);
    }

    [Theory]
    [InlineData(true, true, 0.5f, true)]
    [InlineData(true, true, BattleShipCaptureReplicator.PromotionSettleSeconds, false)]
    [InlineData(false, true, 0.5f, false)]
    [InlineData(true, false, 0.5f, false)]
    public void IsSettling_OnlyForAnInheritedAiHullInsideTheWindow(bool followsHost, bool promoted, float since, bool expected)
    {
        Assert.Equal(expected, BattleShipCaptureReplicator.IsSettling(followsHost, promoted, since));
    }

    // 2.2 case 3: the battle host's own capture of an AI hull it simulates is decided on its next tick (R48 L6: never from the queue
    // drain that overlaps the async agent tick), moves no authority and ends the hull's host following; every client replays it.
    [Fact]
    public void TimerElapsed_OnAnAiHullThisHostSimulates_CapturesOnTheNextTick_WithoutMovingItsAuthority()
    {
        var harness = new Harness();
        var prize = harness.Register(Own, BattleSideEnum.Defender, npc: true);
        var ownHull = harness.Register(Own, BattleSideEnum.Attacker);
        harness.Vacant.Add(prize.Hull);
        var main = Uninitialized<Agent>();
        harness.Engine.Setup(e => e.CrewHullOf(main)).Returns(ownHull.Hull);

        harness.Broker.Publish(this, new ShipCaptureTimerElapsed(prize.Hull, main));
        harness.CaptureEngine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Never);
        harness.Arrivals.ApplyAll();

        Assert.Equal(Own, prize.CurrentAuthority);
        Assert.False(prize.FollowsBattleHost);
        harness.CaptureEngine.Verify(e => e.ApplyCapture(prize.Hull, ownHull.Hull), Times.Once);
        harness.Engine.Verify(e => e.AdoptHull(It.IsAny<MissionObject>()), Times.Never);
        harness.Engine.Verify(e => e.ReleaseHull(It.IsAny<MissionObject>()), Times.Never);
        harness.Network.Verify(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCaptured)), Times.Once);
        harness.Network.Verify(n => n.Send(It.IsAny<string>(), It.Is<IMessage>(m => m is NetworkShipCaptureRequest)), Times.Never);
    }

    // A4: a captor asks the prize's authority alone, once per prize; a second elapsed timer while it waits sends nothing.
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

        harness.Network.Verify(n => n.Send(Host, It.Is<IMessage>(m => m is NetworkShipCaptureRequest
            && ((NetworkShipCaptureRequest)m).CaptorShipId == ownHull.ShipId)), Times.Once);
        harness.Network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        Assert.NotNull(harness.Exchange.PendingRequestAge(prize.ShipId));
    }

    // Q7: no answer within the timeout (the authority left before it decided) tells the player, who can try again.
    [Fact]
    public void PendingRequest_WithNoAnswer_ExpiresAndTellsThePlayer()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var ownHull = harness.Register(Own, BattleSideEnum.Attacker);
        harness.Exchange.RequestCapture(prize, ownHull);

        harness.Exchange.Tick(BattleShipCaptureReplicator.RequestTimeoutSeconds - 0.5f);
        int toldBefore = harness.Told.Count;
        harness.Exchange.Tick(1f);

        Assert.Equal(0, toldBefore);
        Assert.Contains("nobody answered", Assert.Single(harness.Told));
        Assert.Null(harness.Exchange.PendingRequestAge(prize.ShipId));
        Assert.Equal(1L, (long)harness.Inspect()["captureExpired"]!);
    }

    // Q7: a refusal reaches the player once, for the request still waiting.
    [Fact]
    public void Rejection_OfAWaitingRequest_TellsThePlayerOnce()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var ownHull = harness.Register(Own, BattleSideEnum.Attacker);
        harness.Exchange.RequestCapture(prize, ownHull);

        harness.Exchange.AcceptRejection(new NetworkShipCaptureRejected(prize.ShipId, Own, ShipCaptureRefusal.NotVacant));
        harness.Exchange.AcceptRejection(new NetworkShipCaptureRejected(prize.ShipId, Own, ShipCaptureRefusal.NotAuthority));

        Assert.Contains("crew still holds it", Assert.Single(harness.Told));
        Assert.Null(harness.Exchange.PendingRequestAge(prize.ShipId));
    }

    // A3: the broadcast confirm of a rival's capture is what tells a losing captor, locally.
    [Fact]
    public void ConfirmOfARivalsCapture_EndsThisClientsRequest_AndTellsThePlayerItLost()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var ownHull = harness.Register(Own, BattleSideEnum.Attacker);
        var rivalShip = harness.Register(Captor, BattleSideEnum.Attacker);
        harness.Exchange.RequestCapture(prize, ownHull);

        harness.Applier.AcceptConfirm(Confirm(prize, Captor, rivalShip, Host));

        Assert.Null(harness.Exchange.PendingRequestAge(prize.ShipId));
        Assert.Contains("someone captured it first", Assert.Single(harness.Told));
    }

    // Every client replays a confirm from the prize's authority: the captor adopts its copy in place, no swap.
    [Fact]
    public void Confirm_OnTheCaptor_AdoptsItsCopyInPlace()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var ownHull = harness.Register(Own, BattleSideEnum.Attacker);

        harness.Applier.AcceptConfirm(Confirm(prize, Own, ownHull, Host));

        harness.Engine.Verify(e => e.AdoptHull(prize.Hull), Times.Once);
        harness.CaptureEngine.Verify(e => e.ApplyCapture(prize.Hull, ownHull.Hull), Times.Once);
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

        captorSide.Applier.AcceptConfirm(Confirm(prize, Own, ownHull, Host));
        observerSide.Applier.AcceptConfirm(Confirm(observed, Captor, captorCopy, Host));

        captorSide.Relay.Verify(r => r.SendAll(It.Is<IMessage>(m => m is NetworkBattleShipCaptured
            && ((NetworkBattleShipCaptured)m).CampaignShipId == "Ship_7" && ((NetworkBattleShipCaptured)m).InstanceId == "MapEvent_1")),
            Times.Once);
        observerSide.Relay.Verify(r => r.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    // C5: a client that did not decide the capture can't confirm it.
    [Fact]
    public void Confirm_FromAClientThatIsNotThePrizesAuthority_IsDropped()
    {
        var harness = new Harness();
        var prize = harness.Register(Captor, BattleSideEnum.Defender);
        var captorShip = harness.Register(Observer, BattleSideEnum.Attacker);

        harness.Applier.AcceptConfirm(Confirm(prize, Observer, captorShip, Host));

        harness.CaptureEngine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Never);
        Assert.Equal("not_authority", (string?)harness.Inspect()["applied"]!["lastDrop"]);
    }

    // Review r2 item 2: an AI hull's authority moves with host changes this client may not have seen yet, so its confirm is
    // ordered by the capture sequence alone; one from an epoch this client never saw applies instead of dropping.
    [Fact]
    public void Confirm_OfAnAiHull_FromAHostOfAnEpochThisClientNeverSaw_Applies()
    {
        var harness = new Harness(host: Host);
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        harness.Applier.Tick(0.1f);

        harness.Applier.AcceptConfirm(new NetworkShipCaptured(prize.ShipId, Captor, captorShip.ShipId, Successor, 3, 0, 0));

        Assert.Equal(Captor, prize.CaptorControllerId);
        Assert.Equal(Captor, prize.CurrentAuthority);
    }

    // Review r2 item 1: while this client asks to capture another hull with its own, its own hull's next place in the order
    // belongs to that request's decider, so a capture of its own hull is refused as busy here.
    [Fact]
    public void Request_ForTheCaptorHullOfThisClientsPendingRequest_IsBusy()
    {
        var harness = new Harness();
        var target = harness.Register(Observer, BattleSideEnum.Defender);
        var ownHull = harness.Register(Own, BattleSideEnum.Attacker);
        var rivalShip = harness.Register(Captor, BattleSideEnum.Defender);
        harness.Vacant.Add(ownHull.Hull);
        harness.Exchange.RequestCapture(target, ownHull);

        harness.Exchange.AcceptRequest(new NetworkShipCaptureRequest(ownHull.ShipId, Captor, rivalShip.ShipId));
        harness.Exchange.Tick(BattleShipCaptureReplicator.RequestTimeoutSeconds + 0.5f);
        harness.Exchange.AcceptRequest(new NetworkShipCaptureRequest(ownHull.ShipId, Captor, rivalShip.ShipId));

        harness.Network.Verify(n => n.Send(Captor, It.Is<IMessage>(m => m is NetworkShipCaptureRejected
            && ((NetworkShipCaptureRejected)m).Reason == ShipCaptureRefusal.Busy)), Times.Once);
        Assert.Equal(Captor, ownHull.CaptorControllerId);
    }

    // Review r2 item 3: the decider sends its confirm before it applies, so a captor's records sent by the apply never reach a
    // peer ahead of the confirm.
    [Fact]
    public void ApplyDecided_SendsTheConfirmBeforeApplying()
    {
        var harness = new Harness();
        var prize = harness.Register(Own, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        string? captorWhenSent = "unset";
        harness.Network.Setup(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCaptured)))
            .Callback(() => captorWhenSent = prize.CaptorControllerId);

        harness.Applier.ApplyDecided(prize, captorShip, Captor);

        Assert.Null(captorWhenSent);
        Assert.Equal(Captor, prize.CaptorControllerId);
    }

    // PR #4191 M5: a swap that fails part way here records nothing of the capture (registry, order, event or announce) and a
    // repeat of its confirm doesn't replay it; the authority stays with the captor, which still tells the server once.
    [Theory]
    [InlineData(CaptureSwapResult.Failed)]
    [InlineData(CaptureSwapResult.Refused)]
    public void Confirm_WhoseSwapDoesNotComplete_RecordsNothingAndIsNotReplayed(CaptureSwapResult result)
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var ownHull = harness.Register(Own, BattleSideEnum.Attacker);
        harness.CaptureEngine.Setup(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>())).Returns(result);
        harness.Engine.Setup(e => e.CampaignShipIdOf(prize.Hull)).Returns("Ship_7");
        int captureEvents = 0;
        harness.Applier.CaptureApplied += (_, _) => captureEvents++;
        var confirm = Confirm(prize, Own, ownHull, Host);

        harness.Applier.AcceptConfirm(confirm);
        harness.Applier.AcceptConfirm(confirm);

        harness.CaptureEngine.Verify(e => e.ApplyCapture(prize.Hull, ownHull.Hull), Times.Once);
        Assert.Equal(Own, prize.CurrentAuthority);
        Assert.Null(prize.CaptorControllerId);
        Assert.Equal(0, prize.CaptureSequence);
        Assert.False(ownHull.IsOrphaned);
        Assert.Equal(0, ownHull.CaptureSequence);
        Assert.Equal(0, captureEvents);
        Assert.Equal(0, harness.Ledger.DepartedHulls(BattleSideEnum.Defender));
        Assert.False(prize.FollowsBattleHost);
        harness.Relay.Verify(r => r.SendAll(It.Is<IMessage>(m => m is NetworkBattleShipCaptured
            && ((NetworkBattleShipCaptured)m).CampaignShipId == "Ship_7" && ((NetworkBattleShipCaptured)m).InstanceId == "MapEvent_1"
            && ((NetworkBattleShipCaptured)m).CaptureSequence == 1)), Times.Once);
        harness.Relay.VerifyNoOtherCalls();
        var applied = (JObject)harness.Inspect()["applied"]!;
        Assert.Equal(0L, (long)applied["capturesApplied"]!);
        Assert.Equal(1L, (long)applied["swapsFailed"]!);
        Assert.Equal("swap_failed", (string?)applied["lastDrop"]);
    }

    // PR #4191 M5: an AI hull whose capture this client couldn't swap still belongs to its captor, so a later host change
    // leaves it with the captor here as on the peers that applied the capture.
    [Fact]
    public void FailedSwapOfAnAiHull_ThenAHostMigration_LeavesTheHullWithItsCaptor()
    {
        var harness = new Harness(host: Host);
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        harness.CaptureEngine.Setup(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()))
            .Returns(CaptureSwapResult.Failed);

        harness.Applier.AcceptConfirm(Confirm(prize, Captor, captorShip, Host));
        harness.MigrateTo(Successor, epoch: 2);

        Assert.Equal(Captor, prize.CurrentAuthority);
        Assert.Equal(Captor, prize.CommittedCaptorId);
        Assert.Null(prize.CaptorControllerId);
        Assert.False(prize.FollowsBattleHost);
    }

    // C5: the old host's confirm and the host change reach two clients in opposite orders; both end in the same state, since
    // the confirm is checked against the host of its own epoch, not against whoever simulates the hull when it arrives.
    [Fact]
    public void Confirm_AndAHostMigration_InEitherOrder_ConvergeOnEveryClient()
    {
        var confirmFirst = new Harness(host: Host);
        var migrationFirst = new Harness(host: Host);
        var shipId = Guid.NewGuid();
        var captorShipId = Guid.NewGuid();
        foreach (var harness in new[] { confirmFirst, migrationFirst })
        {
            harness.Register(Host, BattleSideEnum.Defender, npc: true, shipId);
            harness.Register(Captor, BattleSideEnum.Attacker, shipId: captorShipId);
            harness.Applier.Tick(0.1f);
        }

        var confirm = new NetworkShipCaptured(shipId, Captor, captorShipId, Host, 1, 0, 0);
        confirmFirst.Applier.AcceptConfirm(confirm);
        confirmFirst.MigrateTo(Successor, epoch: 2);
        migrationFirst.MigrateTo(Successor, epoch: 2);
        migrationFirst.Applier.AcceptConfirm(confirm);

        foreach (var harness in new[] { confirmFirst, migrationFirst })
        {
            Assert.True(harness.Registry.TryGet(shipId, out var prize));
            Assert.Equal(Captor, prize.CurrentAuthority);
            Assert.Equal(Captor, prize.CaptorControllerId);
            Assert.False(prize.FollowsBattleHost);
            Assert.Equal(1, prize.CaptureSequence);
        }
    }

    // C5: captures of a hull from different senders apply in the hulls' capture order whatever order they arrive in: the
    // captor's recapture of its orphan, sent by the captor, waits for the host's capture it builds on.
    [Fact]
    public void Confirms_FromDifferentSenders_ArrivingOutOfOrder_ApplyInCaptureOrder()
    {
        var harness = new Harness(host: Host);
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        harness.Applier.Tick(0.1f);
        var first = new NetworkShipCaptured(prize.ShipId, Captor, captorShip.ShipId, Host, 1, 0, 0);
        var recapture = new NetworkShipCaptured(captorShip.ShipId, Captor, prize.ShipId, Captor, 1, 1, 1);

        harness.Applier.AcceptConfirm(recapture);
        bool recapturedEarly = captorShip.CaptorControllerId != null;
        harness.Applier.AcceptConfirm(first);
        harness.Applier.Tick(0.1f);

        Assert.False(recapturedEarly);
        Assert.False(captorShip.IsOrphaned);
        Assert.Equal(Captor, captorShip.CaptorControllerId);
        Assert.True(prize.IsOrphaned);
        Assert.Equal(2, prize.CaptureSequence);
        Assert.Equal(2, captorShip.CaptureSequence);
        harness.CaptureEngine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Exactly(2));
    }

    // A mesh that delivers at once can bring the captor's answering records back while the confirm is being sent; the capture
    // then applies once here, through them.
    [Fact]
    public void ApplyDecided_WhenTheCaptorsRecordsComeBackDuringTheSend_AppliesOnce()
    {
        var harness = new Harness();
        var prize = harness.Register(Own, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        var record = new BattleShipSpawnData(prize.ShipId, Captor, "MapEventParty_2", true, BattleSideEnum.Attacker, 0,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null, 900f, 400f, 1,
            "", Own, "Ship_7", captorControllerId: Captor, captureSequence: 1, capturedWithShipId: captorShip.ShipId,
            capturedFromSide: BattleSideEnum.Defender);
        harness.Network.Setup(n => n.SendAll(It.Is<IMessage>(m => m is NetworkShipCaptured)))
            .Callback(() => harness.Applier.AcceptRecords(new[] { record }));

        harness.Applier.ApplyDecided(prize, captorShip, Captor);

        harness.CaptureEngine.Verify(e => e.ApplyCapture(prize.Hull, captorShip.Hull), Times.Once);
        Assert.Equal(1, prize.CaptureSequence);
        Assert.Equal(1, captorShip.CaptureSequence);
    }

    // Review r2 item 5: a late repeat of a capture still counts as a repeat once the hull moved on to later captures.
    [Fact]
    public void Confirm_LateRepeat_AfterTheHullMovedOn_IsARepeatNotAConflict()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        var first = new NetworkShipCaptured(prize.ShipId, Captor, captorShip.ShipId, Host, 1, 0, 0);
        harness.Applier.AcceptConfirm(first);
        harness.Applier.AcceptConfirm(new NetworkShipCaptured(captorShip.ShipId, Captor, prize.ShipId, Captor, 1, 1, 1));

        harness.Applier.AcceptConfirm(first);

        var applied = harness.Inspect()["applied"]!;
        Assert.Equal(1L, (long)applied["duplicateConfirms"]!);
        Assert.Equal(0L, (long)applied["captureConflicts"]!);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    public void Conflicts_OnlyWhenADifferentCaptureWasAppliedAtAPlace(bool samePrizePlace, bool knownPrizePlace, bool expected)
    {
        var confirm = new AppliedCapture(Guid.Empty, Guid.Empty, Captor);
        var other = new AppliedCapture(Guid.Empty, Guid.Empty, Observer);
        AppliedCapture? atPrize = knownPrizePlace ? (samePrizePlace ? confirm : other) : (AppliedCapture?)null;
        AppliedCapture? atCaptor = samePrizePlace ? (AppliedCapture?)null : other;

        Assert.Equal(expected, ShipCaptureApplier.Conflicts(atPrize, atCaptor, confirm));
    }

    // A repeat of a capture already applied changes nothing; a different capture at the same place in the order is a
    // conflict: the first applied stands and it is counted.
    [Fact]
    public void Confirm_Repeated_AppliesOnce_AndAConflictAtTheSamePlaceKeepsTheFirst()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        var rivalShip = harness.Register(Observer, BattleSideEnum.Attacker);

        harness.Applier.AcceptConfirm(Confirm(prize, Captor, captorShip, Host));
        harness.Applier.AcceptConfirm(new NetworkShipCaptured(prize.ShipId, Captor, captorShip.ShipId, Host, 1, 0, 0));
        harness.Applier.AcceptConfirm(new NetworkShipCaptured(prize.ShipId, Observer, rivalShip.ShipId, Host, 1, 0, 0));

        harness.CaptureEngine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Once);
        Assert.Equal(Captor, prize.CaptorControllerId);
        var applied = harness.Inspect()["applied"]!;
        Assert.Equal(1L, (long)applied["duplicateConfirms"]!);
        Assert.Equal(1L, (long)applied["captureConflicts"]!);
        Assert.Equal(1, harness.Ledger.DepartedHulls(BattleSideEnum.Defender));
    }

    // C3: a confirm for a hull whose record still waits here (a joiner deploying for longer than any timer) applies once the
    // hull spawned, however long that took.
    [Fact]
    public void Confirm_ForAHullNotRegisteredYet_WaitsForIt_WithoutExpiring()
    {
        var harness = new Harness();
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        var prizeId = Guid.NewGuid();

        harness.Applier.AcceptConfirm(new NetworkShipCaptured(prizeId, Captor, captorShip.ShipId, Host, 1, 0, 0));
        for (int second = 0; second < 120; second++)
            harness.Applier.Tick(1f);
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true, prizeId);
        harness.Applier.Tick(0.1f);

        harness.CaptureEngine.Verify(e => e.ApplyCapture(prize.Hull, captorShip.Hull), Times.Once);
        Assert.Equal(Captor, prize.CaptorControllerId);
    }

    // C3: a record from the captor shows a capture this client missed (it never got the confirm): the registered hull takes it.
    [Fact]
    public void CaptorsRecord_OfARegisteredHull_AppliesTheCaptureThisClientMissed()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        var record = new BattleShipSpawnData(prize.ShipId, Captor, "MapEventParty_2", true, BattleSideEnum.Attacker, 0,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null, 900f, 400f, 1,
            "", Host, "Ship_7", captorControllerId: Captor, captureSequence: 1, capturedWithShipId: captorShip.ShipId,
            capturedFromSide: BattleSideEnum.Defender);

        harness.Applier.AcceptRecords(new[] { record });
        harness.Applier.AcceptRecords(new[] { record });

        harness.CaptureEngine.Verify(e => e.ApplyCapture(prize.Hull, captorShip.Hull), Times.Once);
        Assert.Equal(Captor, prize.CurrentAuthority);
        Assert.Equal(Captor, prize.CaptorControllerId);
        Assert.True(captorShip.IsOrphaned);
        Assert.Equal(1, prize.CaptureSequence);
    }

    // Review r2 item 4: a record of a missed capture waits while its hull is mid-swap here, instead of giving up.
    [Fact]
    public void CaptorsRecord_OfAHullMidSwap_WaitsForTheSwap()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);
        prize.CurrentAuthority = Own;
        harness.HullAuthority.Replace(prize.ShipId);
        var record = new BattleShipSpawnData(prize.ShipId, Captor, "MapEventParty_2", true, BattleSideEnum.Attacker, 0,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), "northern_light_ship", null, null, null, null, 900f, 400f, 1,
            "", Host, "Ship_7", captorControllerId: Captor, captureSequence: 1, capturedWithShipId: captorShip.ShipId,
            capturedFromSide: BattleSideEnum.Defender);

        harness.Applier.AcceptRecords(new[] { record });
        bool appliedMidSwap = prize.CaptorControllerId != null;
        harness.HullAuthority.Dispose();
        harness.Applier.Tick(0.1f);

        Assert.False(appliedMidSwap);
        Assert.Equal(Captor, prize.CaptorControllerId);
        Assert.Equal(1, prize.CaptureSequence);
    }

    // F8: a request that expired before its own confirm arrived doesn't tell the player someone else captured the hull.
    [Fact]
    public void ConfirmOfThisClientsOwnCapture_AfterItsRequestExpired_TellsNothingMore()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var ownHull = harness.Register(Own, BattleSideEnum.Attacker);
        harness.Exchange.RequestCapture(prize, ownHull);
        harness.Exchange.Tick(BattleShipCaptureReplicator.RequestTimeoutSeconds + 0.5f);

        harness.Applier.AcceptConfirm(Confirm(prize, Own, ownHull, Host));

        Assert.Contains("nobody answered", Assert.Single(harness.Told));
        Assert.Equal(Own, prize.CaptorControllerId);
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

        harness.Applier.AcceptConfirm(Confirm(prize, Captor, captorShip, Host));
        bool appliedMidSwap = prize.CaptorControllerId != null;
        harness.HullAuthority.Dispose();
        harness.Applier.Tick(0.1f);

        Assert.False(appliedMidSwap);
        Assert.Equal(Captor, prize.CaptorControllerId);
    }

    // Q5: capturing the last enemy hull counts it gone from its side, so vanilla's retreat rule ends the battle there.
    [Fact]
    public void Capture_CountsThePrizeGoneFromItsOldSide()
    {
        var harness = new Harness();
        var prize = harness.Register(Host, BattleSideEnum.Defender, npc: true);
        var captorShip = harness.Register(Captor, BattleSideEnum.Attacker);

        harness.Applier.AcceptConfirm(Confirm(prize, Captor, captorShip, Host));

        Assert.Equal(1, harness.Ledger.DepartedHulls(BattleSideEnum.Defender));
        Assert.Equal(0, harness.Ledger.DepartedHulls(BattleSideEnum.Attacker));
#if COOP_NAVAL
        Assert.True(global::Missions.Naval.CoopNavalBattleEndLogic.KeepsRetreat(harness.Ledger.DepartedHulls(BattleSideEnum.Defender),
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

        harness.Applier.AcceptConfirm(new NetworkShipCaptured(prize.ShipId, Captor, captorShipId, Host, 1, 0, 0));

        Assert.False(harness.Registry.TryGet(prize.ShipId, out _));
        harness.CaptureEngine.Verify(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, CaptureOrder.Next)]
    [InlineData(0, 0, 1, 0, CaptureOrder.Ahead)]
    [InlineData(0, 0, 0, 2, CaptureOrder.Ahead)]
    [InlineData(1, 0, 0, 0, CaptureOrder.Behind)]
    [InlineData(1, 1, 1, 0, CaptureOrder.Behind)]
    public void OrderOf_PlacesAConfirmAgainstBothHullsCounts(int localPrize, int localCaptor, int prize, int captor, CaptureOrder expected)
    {
        Assert.Equal(expected, ShipCaptureApplier.OrderOf(localPrize, localCaptor, prize, captor));
    }

    private static NetworkShipCaptured Confirm(NetworkShipInfo prize, string captor, NetworkShipInfo captorShip, string sender) =>
        new NetworkShipCaptured(prize.ShipId, captor, captorShip.ShipId, sender, 1, prize.CaptureSequence, captorShip.CaptureSequence);

    private static T Uninitialized<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    // The real ship replicator, hull authority and registry over mocked engines, with both capture services under test.
    private sealed class Harness
    {
        public Mock<IBattleNetwork> Network { get; } = new Mock<IBattleNetwork>();
        public Mock<INetwork> Relay { get; } = new Mock<INetwork>();
        public Mock<INavalShipEngine> Engine { get; } = new Mock<INavalShipEngine>();
        public Mock<INavalShipCaptureEngine> CaptureEngine { get; } = new Mock<INavalShipCaptureEngine>();
        public Mock<IBattleSession> Session { get; } = new Mock<IBattleSession>();
        public MessageBroker Broker { get; } = new MessageBroker();
        public NetworkShipRegistry Registry { get; } = new NetworkShipRegistry();
        public List<string> Members { get; } = new List<string> { Host, Captor, Observer, Successor };
        public HashSet<MissionObject> Vacant { get; } = new HashSet<MissionObject>();
        public List<string> Told { get; } = new List<string>();
        public List<MissionObject> LiveHulls { get; } = new List<MissionObject>();
        public NavalHullAuthority HullAuthority { get; }
        public BattleShipArrivals Arrivals { get; } = new BattleShipArrivals();
        public BattleShipReplicator Ships { get; }
        public ShipCaptureApplier Applier { get; }
        public NavalSideHullLedger Ledger { get; }
        public BattleShipCaptureReplicator Exchange { get; }
        private readonly Dictionary<MissionObject, BattleSideEnum> sides = new Dictionary<MissionObject, BattleSideEnum>();
        private int hostEpoch = 1;
        private string host;

        public Harness(string host = null)
        {
            this.host = host;
            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(c => c.ShipRegistry).Returns(Registry);
            component.SetupGet(c => c.AgentMovementHandler).Returns(Mock.Of<IAgentMovementHandler>());

            Session.SetupGet(s => s.OwnControllerId).Returns(Own);
            Session.SetupGet(s => s.HostEpoch).Returns(() => hostEpoch);
            Session.SetupGet(s => s.HostControllerId).Returns(() => this.host);
            Session.SetupGet(s => s.IsLocalHost).Returns(() => this.host == Own);
            Session.SetupGet(s => s.InstanceId).Returns("MapEvent_1");
            Session.Setup(s => s.IsOwn(It.IsAny<string>())).Returns((string id) => id == Own);
            Session.SetupGet(s => s.IsNaval).Returns(true);
            Session.SetupGet(s => s.HasInstance).Returns(true);

            Engine.SetupGet(e => e.Hulls).Returns(() => LiveHulls);
            Engine.Setup(e => e.AgentsOnDeck(It.IsAny<MissionObject>())).Returns(Array.Empty<Agent>());
            Engine.Setup(e => e.ParkHullAgents(It.IsAny<MissionObject>())).Returns(Array.Empty<HullSwapAgent>());
            Engine.Setup(e => e.SideOf(It.IsAny<MissionObject>())).Returns((MissionObject hull) =>
                sides.TryGetValue(hull, out var side) ? side : BattleSideEnum.None);
            CaptureEngine.Setup(e => e.IsVacantForCapture(It.IsAny<MissionObject>())).Returns((MissionObject hull) => Vacant.Contains(hull));
            CaptureEngine.Setup(e => e.CanBeTakenOver(It.IsAny<MissionObject>())).Returns(true);
            CaptureEngine.Setup(e => e.ApplyCapture(It.IsAny<MissionObject>(), It.IsAny<MissionObject>()))
                .Callback((MissionObject prize, MissionObject captor) =>
                {
                    sides[prize] = sides[captor];
                    sides[captor] = BattleSideEnum.None;
                });

            var members = new Mock<IMissionContext>();
            members.SetupGet(m => m.ControllersInMission).Returns(() => Members.ToArray());
            var context = ShipReplicatorFixture.Context(Session.Object, component.Object, members.Object);
            var core = new ShipReplicatorFixture(context, Broker, Engine.Object, Network.Object, captureEngine: CaptureEngine.Object,
                arrivals: Arrivals);
            HullAuthority = core.HullAuthority;
            Ships = core.Replicator;
            Ledger = core.Ledger;
            Applier = new ShipCaptureApplier(Network.Object, Relay.Object, Broker, context, Engine.Object, CaptureEngine.Object, Ships,
                HullAuthority, Arrivals, Ledger);
            Exchange = new BattleShipCaptureReplicator(Network.Object, Broker, context, Engine.Object, CaptureEngine.Object,
                HullAuthority, Applier, Arrivals);
            Broker.Subscribe<SendInformationMessage>(Handle_Told);
        }

        // Keeps the told lines; an instance method, so the broker's weak delegate stays alive with the harness.
        private void Handle_Told(MessagePayload<SendInformationMessage> payload) => Told.Add(payload.What.Text);

        // A hull on a side, registered under its authority with a formation of its own.
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

        /// <summary>This client becomes the battle host at <paramref name="epoch"/>.</summary>
        public void Promote(int epoch)
        {
            hostEpoch = epoch;
            host = Own;
        }

        /// <summary>Another member becomes the battle host at <paramref name="epoch"/>, and the AI hulls move to it.</summary>
        public void MigrateTo(string newHost, int epoch)
        {
            hostEpoch = epoch;
            host = newHost;
            Ships.TransferHostHulls(newHost, epoch);
        }

        public JObject Inspect() => JObject.FromObject(Exchange.Inspect());
    }
}
