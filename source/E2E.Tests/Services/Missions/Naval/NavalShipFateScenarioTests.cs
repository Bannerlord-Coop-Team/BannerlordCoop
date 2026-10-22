using Common.Messaging;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.Players;
using GameInterface.Services.Ships.Data;
using GameInterface.Services.Ships.Messages;
using Missions.Messages;
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
        var hull = Damage(battle, a, shipId, hitPoints: 640f, sailHitPoints: 210f);

        a.Tick(BattleShipFateReporterInterval);
        Assert.Equal(1, a.Services.FateReporter.FatesSentOf(hull));
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
        SinkHull(a, sunkId);
        a.Hull(survivor).HitPoints = 450f;
        a.Tick();

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

    private const float BattleShipFateReporterInterval = 2f;

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
