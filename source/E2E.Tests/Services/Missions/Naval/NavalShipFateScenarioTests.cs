using Common.Messaging;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.Players;
using GameInterface.Services.Ships.Data;
using Common.Network.Messages;
using Missions.Battles;
using Missions.Messages;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// M6 D4/D5: the server holds the ship fates the simulating clients report and writes them to the campaign ships with patches
/// live, so each write reaches every client: at a member's departure, at the result commit before the loot reads the ships,
/// and when the battle empties.
/// </summary>
public class NavalShipFateScenarioTests : NavalBattleTestEnvironment
{
    private const float MaxHitPoints = 1000f;
    private const float MaxSailHitPoints = 500f;

    public NavalShipFateScenarioTests(ITestOutputHelper output) : base(output, numClients: 2) { }

    [Fact]
    public void Departure_WritesTheOwnShipsBattleCondition_ToEveryMachine_BeforeTheConclusion()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out _, out var concluded);
        var shipId = GiveShip(PlayerPartyOf("A"));
        Damage(battle, a, shipId, hitPoints: 640f, sailHitPoints: 210f);

        a.Tick(BattleShipFateReporterInterval);
        LeaveMission(battle, a);

        Assert.Equal(0, concluded());
        AssertShipEverywhere(shipId, ship =>
        {
            Assert.Equal(640f, ship.HitPoints, 2);
            Assert.Equal(210f, ship.SailHitPoints, 2);
        });
    }

    [Fact]
    public void SunkShip_GoesOutAtOnce_AndLeavesItsPartyEverywhereWhenItsOwnerDeparts()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out _, out _);
        var partyId = PlayerPartyOf("A");
        var shipId = GiveShip(partyId);
        FieldSunk(battle, a, shipId);

        a.Tick();
        LeaveMission(battle, a);

        AssertDestroyedEverywhere(shipId, partyId);
    }

    // M6 review C1: a player's last fates and its disconnect can arrive in one poll batch, and the disconnect clears its peer
    // before the game thread records them; the sender is resolved as they arrive, so they still apply at its departure.
    [Fact]
    public void Fates_FollowedByADisconnectInTheSameBatch_StillApplyAtTheDeparture()
    {
        using var battle = StartServerBattle(atSea: true, out _, out var b, out _);
        var partyId = PlayerPartyOf("B");
        var shipId = GiveShip(partyId);

        // Both arrive on the poll thread, so the record waits on the game thread queue behind the disconnect's peer clear.
        Server.SimulateMessage(b.Instance.NetPeer, new NetworkBattleShipFates(battle.MapEventId, 1, 1,
            new[] { new BattleShipFate(shipId, 0f, 100f, sunk: true) }), markGameThread: false);
        Server.SimulateMessage(this, new PlayerDisconnected(b.Instance.NetPeer, default), markGameThread: false);
        b.Instance.Call(b.Network.Stop);

        AssertDestroyedEverywhere(shipId, partyId);
    }

    // The commit runs at the start of OnBattleWon, so the loot (CommitMapEventResults) finds sunk AI ships destroyed and
    // survivors at their battle HP; vanilla's after-defeat damage then comes on top of that.
    [Fact]
    public void Commit_DestroysTheSunkAiShip_AndTheSurvivorReachesTheLootAtItsBattleHp()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out _, out var concluded);
        var aiPartyId = AddAiParty(battle);
        var sunkId = GiveShip(aiPartyId);
        var survivorId = GiveShip(aiPartyId);
        battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f), campaignShipId: sunkId);
        var survivor = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(40f, 300f, 0f), campaignShipId: survivorId);
        battle.CommitAll();
        battle.TickAll();
        // No tick after this: the result report's flush carries both fates.
        SinkHull(a, sunkId);
        a.Hull(survivor).HitPoints = 450f;

        float? survivorHpAtLoot = null;
        bool sunkGoneAtLoot = false;
        Action<MessagePayload<CommitMapEventResults>> onCommit = payload =>
        {
            survivorHpAtLoot = Server.GetRegisteredObject<Ship>(survivorId).HitPoints;
            sunkGoneAtLoot = !Server.ObjectManager.TryGetObject<Ship>(sunkId, out _);
        };
        Server.Resolve<IMessageBroker>().Subscribe(onCommit);
        ResolveOnHost(a, BattleState.AttackerVictory);
        GC.KeepAlive(onCommit);

        Assert.Equal(1, concluded());
        Assert.Equal(450f, survivorHpAtLoot!.Value, 2);
        Assert.True(sunkGoneAtLoot);
        foreach (var client in Clients)
            Assert.False(client.ObjectManager.TryGetObject<Ship>(sunkId, out _));
    }

    // M6 D4: a battle that finalizes without a result commit (abandoned, orphaned) still writes what its members reported,
    // from the finalize postfix while the map event is still registered.
    [Fact]
    public void FinalizeWithoutACommit_WritesTheHeldFates_ToEveryMachine()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out _, out var concluded);
        var shipId = GiveShip(PlayerPartyOf("A"));
        SendFates(a, battle, hostEpoch: 1, revision: 1, new BattleShipFate(shipId, 520f, 130f, false));

        Server.Call(() => Server.GetRegisteredObject<MapEvent>(battle.MapEventId).FinalizeEvent(), MapEventDisabledMethods);

        Assert.Equal(0, concluded());
        AssertShipEverywhere(shipId, ship =>
        {
            Assert.Equal(520f, ship.HitPoints, 2);
            Assert.Equal(130f, ship.SailHitPoints, 2);
        });
    }

    // A promoted host numbers its AI fates from one again at the newer epoch, and the departed host's late fates are refused.
    [Fact]
    public void AiFates_AfterAHostChange_KeepTheNewestEpoch_AndTheEmptiedBattleAppliesThem()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out _);
        var aiPartyId = AddAiParty(battle);
        var shipId = GiveShip(aiPartyId);

        SendFates(a, battle, hostEpoch: 1, revision: 5, new BattleShipFate(shipId, 800f, 400f, false));
        Server.Call(() => Server.Resolve<IBattleHostRegistry>().Set(battle.MapEventId,
            new BattleHostAssignment("B", Array.Empty<string>(), 2, isNaval: true)));
        SendFates(b, battle, hostEpoch: 2, revision: 1, new BattleShipFate(shipId, 600f, 300f, false));
        SendFates(a, battle, hostEpoch: 1, revision: 9, new BattleShipFate(shipId, 900f, 450f, false));

        DepartBattle("B", battle.MapEventId, isInstanceEmpty: true);

        AssertShipEverywhere(shipId, ship => Assert.Equal(600f, ship.HitPoints, 2));
    }

    // BR-076: once the results were calculated a fate changes nothing, whatever its epoch, so a second result report or a
    // member's last flush can't write the ships a second time.
    [Fact]
    public void LateFates_AfterTheResultsWereCalculated_AreDropped()
    {
        using var battle = StartServerBattle(atSea: true, out _, out var b, out _);
        var shipId = GiveShip(PlayerPartyOf("B"));
        Server.Call(() => Server.GetRegisteredObject<MapEvent>(battle.MapEventId)._mapEventResultsCalculated = true);

        SendFates(b, battle, hostEpoch: 2, revision: 1, new BattleShipFate(shipId, 100f, 50f, true));
        DepartBattle("B", battle.MapEventId, isInstanceEmpty: true);

        AssertShipEverywhere(shipId, ship => Assert.Equal(MaxHitPoints, ship.HitPoints, 2));
    }

    // A fate from a member that already left the battle, or for another player's ship, is refused.
    [Fact]
    public void Fates_FromALeftMemberOrForAnotherPlayersShip_AreDropped()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out _);
        var shipOfB = GiveShip(PlayerPartyOf("B"));
        var shipOfA = GiveShip(PlayerPartyOf("A"));
        SendFates(a, battle, hostEpoch: 1, revision: 1, new BattleShipFate(shipOfB, 100f, 50f, false));
        LeaveMission(battle, a);
        SendFates(a, battle, hostEpoch: 1, revision: 2, new BattleShipFate(shipOfA, 100f, 50f, false));

        DepartBattle("B", battle.MapEventId, isInstanceEmpty: true);

        AssertShipEverywhere(shipOfB, ship => Assert.Equal(MaxHitPoints, ship.HitPoints, 2));
        AssertShipEverywhere(shipOfA, ship => Assert.Equal(MaxHitPoints, ship.HitPoints, 2));
    }

    // A returning player reads the ships its departure wrote, and its fresh reporter numbering from one again still lands,
    // because the departure applied and cleared its earlier fates.
    [Fact]
    public void Rejoin_ReadsTheDepartureWrite_AndItsRestartedRevisionsStillApply()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out _, out _);
        var partyId = PlayerPartyOf("A");
        var shipId = GiveShip(partyId);
        Damage(battle, a, shipId, hitPoints: 640f, sailHitPoints: 210f);
        a.Tick(BattleShipFateReporterInterval);
        LeaveMission(battle, a);
        a.Call(() => Assert.Equal(640f, a.Instance.GetRegisteredObject<Ship>(shipId).HitPoints, 2));

        var returned = battle.Rejoin(a);
        EnterBattle(returned.Instance, battle.MapEventId);
        Server.Call(() => Server.SimulateMessage(returned.Instance.NetPeer, new NetworkMissionEntered("A", battle.MapEventId)));
        Damage(battle, returned, shipId, hitPoints: 640f, sailHitPoints: 210f);
        SinkHull(returned, shipId);
        returned.Tick();
        LeaveMission(battle, returned);

        AssertDestroyedEverywhere(shipId, partyId);
    }

    // M6 D7: the owner's hits sum the damage the ship's upgrades mitigated; the server grants it once through vanilla's per-hit
    // skill call (NavalSkillLevellingManager turns it into Boatswain XP), before it writes the ship.
    [Fact]
    public void BoatswainXp_ForTheBattlesMitigatedDamage_IsGrantedOnceAtApply()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out _, out _);
        var shipId = GiveShip(PlayerPartyOf("A"));
        var hull = Damage(battle, a, shipId, hitPoints: 640f, sailHitPoints: 210f);
        a.Hull(hull).MitigatedDamage = 30f;
        a.Tick(BattleShipFateReporterInterval);
        a.Hull(hull).MitigatedDamage = 50f;
        a.Tick(BattleShipFateReporterInterval);

        var grants = new List<(Ship Ship, float RawDamage, float FinalDamage)>();
        var skills = new Mock<ISkillLevelingManager>();
        skills.Setup(manager => manager.OnShipDamaged(It.IsAny<Ship>(), It.IsAny<float>(), It.IsAny<float>()))
            .Callback<Ship, float, float>((ship, raw, final) => grants.Add((ship, raw, final)));
        ISkillLevelingManager original = null!;
        Server.Call(() =>
        {
            original = Campaign.Current.SkillLevelingManager;
            Campaign.Current.SkillLevelingManager = skills.Object;
        });
        try
        {
            LeaveMission(battle, a);
            DepartBattle("B", battle.MapEventId, isInstanceEmpty: true);
        }
        finally
        {
            Server.Call(() => Campaign.Current.SkillLevelingManager = original);
        }

        var grant = Assert.Single(grants);
        Server.Call(() => Assert.Same(Server.GetRegisteredObject<Ship>(shipId), grant.Ship));
        Assert.Equal(50f, grant.RawDamage, 2);
        Assert.Equal(0f, grant.FinalDamage);
    }

    // Capture 2.5: once B captured an AI hull the server takes that ship's fates from B alone, so the old host's late fate is
    // refused and the commit hands the loot the captor's battle HP.
    [Fact]
    public void CapturedAiHull_ItsCaptorsFateAppliesAtTheCommit_AndTheOldHostsLateFateIsDropped()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out var concluded);
        var shipId = GiveShip(AddAiParty(battle));
        var prize = CaptureAiHull(battle, a, b, shipId);
        b.Hull(prize).HitPoints = 450f;
        b.Tick(BattleShipFateReporterInterval);
        SendFates(a, battle, hostEpoch: 1, revision: 99, new BattleShipFate(shipId, 900f, 450f, false));

        float? hpAtLoot = null;
        Action<MessagePayload<CommitMapEventResults>> onCommit = _ => hpAtLoot = Server.GetRegisteredObject<Ship>(shipId).HitPoints;
        Server.Resolve<IMessageBroker>().Subscribe(onCommit);
        ResolveOnHost(a, BattleState.AttackerVictory);
        GC.KeepAlive(onCommit);

        Assert.Equal(1, concluded());
        Assert.Equal(450f, hpAtLoot!.Value, 2);
    }

    // Capture 2.6 (M6 D5 widened): the prize leaves with its captor, so the captor's departure writes the prize's battle HP
    // to the AI party's ship on every machine before the battle concludes.
    [Fact]
    public void CapturedAiHull_TheCaptorsDepartureWritesItsBattleCondition_ToEveryMachine()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out var concluded);
        var shipId = GiveShip(AddAiParty(battle));
        var prize = CaptureAiHull(battle, a, b, shipId);
        b.Hull(prize).HitPoints = 520f;
        b.Hull(prize).SailHitPoints = 130f;

        b.Tick(BattleShipFateReporterInterval);
        LeaveMission(battle, b);

        Assert.Equal(0, concluded());
        AssertShipEverywhere(shipId, ship =>
        {
            Assert.Equal(520f, ship.HitPoints, 2);
            Assert.Equal(130f, ship.SailHitPoints, 2);
        });
    }

    // PR #4191 M5: the captor's swap failed, but the capture was decided and it still simulates the prize, so it still tells
    // the server; its later fate of the AI hull is accepted and its departure writes it.
    [Fact]
    public void CapturedAiHull_WhoseSwapFailedAtTheCaptor_TheCaptorsLaterFateStillApplies()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out var concluded);
        var shipId = GiveShip(AddAiParty(battle));
        var prize = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f), formationIndex: 0, campaignShipId: shipId);
        var captorHull = battle.FieldPlayerHull(b, new Vec3(0f, 0f, 0f), formationIndex: 0);
        battle.CommitAll();
        battle.TickAll();
        a.Hull(prize).IsVacant = true;
        b.Engine.CaptureResult = CaptureSwapResult.Failed;
        battle.ExpectLoggedFailure("the capture isn't recorded here");

        b.ElapseCaptureTimer(prize, captorHull);
        Assert.Equal(b.Id, b.Ship(prize).CurrentAuthority);
        Assert.Null(b.Ship(prize).CaptorControllerId);
        b.Hull(prize).HitPoints = 520f;
        b.Hull(prize).SailHitPoints = 130f;
        b.Tick(BattleShipFateReporterInterval);
        LeaveMission(battle, b);

        Assert.Equal(0, concluded());
        AssertShipEverywhere(shipId, ship =>
        {
            Assert.Equal(520f, ship.HitPoints, 2);
            Assert.Equal(130f, ship.SailHitPoints, 2);
        });
    }

    // C4: a PvP victim can leave right after confirming, before its captor's report reaches the server; the capture is still
    // recorded, so the captor's fates of the ship count and its departure writes them.
    [Fact]
    public void CaptureOfAShipWhoseOwnerAlreadyLeft_IsStillRecorded_AndTheCaptorsFateApplies()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out _, out _);
        var victimPartyId = PlayerPartyOf("B");
        var shipId = GiveShip(victimPartyId);
        Server.Call(() => Server.GetRegisteredObject<MobileParty>(victimPartyId).Party.MapEventSide = null, MapEventDisabledMethods);

        Server.Call(() => Server.SimulateMessage(a.Instance.NetPeer, new NetworkBattleShipCaptured(battle.MapEventId, shipId, 1)));
        SendFates(a, battle, hostEpoch: 1, revision: 1, new BattleShipFate(shipId, 480f, 120f, false));
        LeaveMission(battle, a);

        AssertShipEverywhere(shipId, ship =>
        {
            Assert.Equal(480f, ship.HitPoints, 2);
            Assert.Equal(120f, ship.SailHitPoints, 2);
        });
    }

    // R46 bug 4: a capture never changes the campaign owner, so the victim's departure must leave the prize's fate held; it
    // leaves with its captor, whose departure writes it.
    [Fact]
    public void CapturedPlayerShip_TheVictimsDepartureLeavesItsFate_AndTheCaptorsDepartureWritesIt()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out _);
        var shipId = GiveShip(PlayerPartyOf("B"));

        Server.Call(() => Server.SimulateMessage(a.Instance.NetPeer, new NetworkBattleShipCaptured(battle.MapEventId, shipId, 1)));
        SendFates(a, battle, hostEpoch: 1, revision: 1, new BattleShipFate(shipId, 480f, 120f, false));
        LeaveMission(battle, b);

        AssertShipEverywhere(shipId, ship =>
        {
            Assert.Equal(MaxHitPoints, ship.HitPoints, 2);
            Assert.Equal(MaxSailHitPoints, ship.SailHitPoints, 2);
        });

        LeaveMission(battle, a);

        AssertShipEverywhere(shipId, ship =>
        {
            Assert.Equal(480f, ship.HitPoints, 2);
            Assert.Equal(120f, ship.SailHitPoints, 2);
        });
    }

    // The host A fields the AI party's hull for the ship; B captures it with its own hull, which the host confirms.
    // R48 re-review: a confirm still waiting in the captor's arrivals when its result report goes out is applied first, so the
    // captor tells the server of the capture ahead of its result, and the prize's fate follows the captor.
    [Fact]
    public void ConfirmStillQueuedAtTheCaptorsResultReport_IsAppliedFirst_AndReportedAheadOfTheResult()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out _);
        var prize = QueueCaptureConfirm(battle, a, b);
        b.Call(() =>
        {
            b.Controller.ResultCommitter.AcceptResolvedState(BattleState.AttackerVictory);
            b.Controller.ResultCommitter.ReportAcceptedResult();
        }, VictoryConclusionDisabledMethods());

        Assert.Equal("B", b.Ship(prize).CaptorControllerId);
        var sent = b.Instance.NetworkSentMessages.Messages.ToList();
        int captureAt = sent.FindIndex(message => message is NetworkBattleShipCaptured);
        int resultAt = sent.FindIndex(message => message is NetworkBattleResultReady);
        Assert.True(captureAt >= 0 && resultAt > captureAt, $"capture report at {captureAt}, result at {resultAt}");
    }

    // S3 D5: a result report from outside the mission tick while the async agent tick runs applies nothing, so the queued
    // confirm waits for the next mission tick and the capture report follows the result.
    [Fact]
    public void ConfirmStillQueuedAtTheCaptorsResultReport_DuringTheAsyncAgentTick_WaitsForTheNextTick()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out _);
        var prize = QueueCaptureConfirm(battle, a, b);
        b.AsyncAgentTick.IsRunning = true;
        b.Call(() =>
        {
            b.Controller.ResultCommitter.AcceptResolvedState(BattleState.AttackerVictory);
            b.Controller.ResultCommitter.ReportAcceptedResult();
        }, VictoryConclusionDisabledMethods());

        Assert.Null(b.Ship(prize).CaptorControllerId);
        Assert.DoesNotContain(b.Instance.NetworkSentMessages.Messages, message => message is NetworkBattleShipCaptured);

        b.AsyncAgentTick.IsRunning = false;
        b.Tick();
        Assert.Equal("B", b.Ship(prize).CaptorControllerId);
    }

    // S3 D5: only this battle's result report applies the arrivals early; another instance's leaves them queued.
    [Fact]
    public void ResultReportOfAnotherInstance_LeavesTheQueuedConfirmForTheNextTick()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out _);
        var prize = QueueCaptureConfirm(battle, a, b);

        b.Call(() => b.Broker.Publish(b, new BattleResultReporting("another battle")));

        Assert.Null(b.Ship(prize).CaptorControllerId);
        b.Tick();
        Assert.Equal("B", b.Ship(prize).CaptorControllerId);
    }

    // The host A fields the AI party's hull and B claims it with its own hull; the host's confirm waits in B's arrivals.
    private Guid QueueCaptureConfirm(NavalBattle battle, NavalPeer host, NavalPeer captor)
    {
        var shipId = GiveShip(AddAiParty(battle));
        var prize = battle.FieldAiHull(host, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f), formationIndex: 0, campaignShipId: shipId);
        var captorHull = battle.FieldPlayerHull(captor, new Vec3(0f, 0f, 0f), formationIndex: 0);
        battle.CommitAll();
        battle.TickAll();
        host.Hull(prize).IsVacant = true;
        captor.SettleMesh = null;

        captor.ElapseCaptureTimer(prize, captorHull);
        host.TickArrivals();
        battle.DeliverMesh();
        Assert.Null(captor.Ship(prize).CaptorControllerId);
        return prize;
    }


    private static Guid CaptureAiHull(NavalBattle battle, NavalPeer host, NavalPeer captor, string shipId)
    {
        var prize = battle.FieldAiHull(host, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f), formationIndex: 0, campaignShipId: shipId);
        var captorHull = battle.FieldPlayerHull(captor, new Vec3(0f, 0f, 0f), formationIndex: 0);
        battle.CommitAll();
        battle.TickAll();
        host.Hull(prize).IsVacant = true;

        captor.ElapseCaptureTimer(prize, captorHull);
        Assert.Equal(captor.Id, captor.Ship(prize).CaptorControllerId);
        return prize;
    }

    private const float BattleShipFateReporterInterval = BattleShipFateReporter.ReportIntervalSeconds;

    // A registered ship at full condition, given to the party through the vanilla production ownership path on the server.
    private string GiveShip(string mobilePartyId)
    {
        var shipId = CreateRegisteredObject<Ship>();
        foreach (var instance in new[] { Server }.Concat(Clients))
            instance.Call(() =>
            {
                var hull = instance.GetRegisteredObject<Ship>(shipId).ShipHull;
                hull.MaxHitPoints = (int)MaxHitPoints;
                hull.MaxSailHitPoints = (int)MaxSailHitPoints;
            });
        Server.Call(() =>
        {
            var ship = Server.GetRegisteredObject<Ship>(shipId);
            ChangeShipOwnerAction.ApplyByProduction(Server.GetRegisteredObject<MobileParty>(mobilePartyId).Party, ship);
            ship.HitPoints = MaxHitPoints;
            ship.SailHitPoints = MaxSailHitPoints;
        });
        return shipId;
    }

    private string PlayerPartyOf(string controllerId)
    {
        string partyId = null!;
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(controllerId, out var player));
            partyId = player.MobilePartyId;
        });
        return partyId;
    }

    // An AI party on the defender side; the battle's pirate party is registered like a player, so it cannot stand for one.
    private string AddAiParty(NavalBattle battle)
    {
        var partyId = CreateRegisteredObject<MobileParty>();
        Server.Call(() => Server.GetRegisteredObject<MobileParty>(partyId).Party.MapEventSide =
            Server.GetRegisteredObject<MapEvent>(battle.MapEventId).DefenderSide, MapEventDisabledMethods);
        return partyId;
    }

    // The owner's hull of the ship, fielded and committed, then damaged to the given condition.
    private static Guid Damage(NavalBattle battle, NavalPeer owner, string shipId, float hitPoints, float sailHitPoints)
    {
        var hull = battle.FieldPlayerHull(owner, Vec3.Zero, shipId);
        owner.Commit();
        owner.Tick();
        var fake = owner.Hull(hull);
        fake.HitPoints = hitPoints;
        fake.SailHitPoints = sailHitPoints;
        return hull;
    }

    // The owner's hull of the ship, fielded and committed, then sunk.
    private static void FieldSunk(NavalBattle battle, NavalPeer owner, string shipId)
    {
        battle.FieldPlayerHull(owner, Vec3.Zero, shipId);
        owner.Commit();
        owner.Tick();
        SinkHull(owner, shipId);
    }

    // The owner's simulated hull standing for the ship goes under.
    private static void SinkHull(NavalPeer owner, string shipId)
    {
        var fake = owner.Engine.AllHulls.Single(hull => hull.CampaignShipId == shipId && !hull.IsRemoved && !hull.IsCopy);
        fake.HitPoints = 0f;
        fake.SinkingState = BattleShipCondition.Sunk;
    }

    private void SendFates(NavalPeer sender, NavalBattle battle, int hostEpoch, long revision, params BattleShipFate[] fates) =>
        Server.Call(() => Server.SimulateMessage(sender.Instance.NetPeer,
            new NetworkBattleShipFates(battle.MapEventId, hostEpoch, revision, fates)));

    // The member's mission ends and the server observes it leave.
    private void LeaveMission(NavalBattle battle, NavalPeer leaver)
    {
        leaver.Call(leaver.Network.Stop);
        Server.Call(() => Server.SimulateMessage(leaver.Instance.NetPeer, new NetworkMissionLeft(leaver.Id, battle.MapEventId)),
            VictoryConclusionDisabledMethods());
    }

    private void AssertShipEverywhere(string shipId, Action<Ship> assert)
    {
        foreach (var instance in new[] { Server }.Concat(Clients))
        {
            instance.PumpGameThread();
            instance.Call(() => assert(instance.GetRegisteredObject<Ship>(shipId)));
        }
    }

    private void AssertDestroyedEverywhere(string shipId, string partyId)
    {
        foreach (var instance in new[] { Server }.Concat(Clients))
        {
            instance.PumpGameThread();
            instance.Call(() =>
            {
                Assert.False(instance.ObjectManager.TryGetObject<Ship>(shipId, out _));
                Assert.DoesNotContain(instance.GetRegisteredObject<MobileParty>(partyId).Ships,
                    ship => instance.ObjectManager.TryGetId(ship, out var id) && id == shipId);
            });
        }
    }
}
