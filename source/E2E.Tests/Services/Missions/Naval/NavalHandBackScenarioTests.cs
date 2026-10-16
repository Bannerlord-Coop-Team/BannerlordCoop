using GameInterface.Services.MapEvents.TroopSupply;
using Missions.Battles;
using Missions.Messages;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// M8b: a player who disconnected comes back into the running battle. Its launcher fields its ship again; that own hull
/// takes over the ship id the battle host adopted, at the adopted hull's frame and condition, once the host released it.
/// The host keeps no simulated twin and stops streaming the hull; observers follow the returning owner. A hull that sank
/// while adopted is not handed back, and a repeated or foreign reclaim is refused.
/// </summary>
public class NavalHandBackScenarioTests : NavalBattleTestEnvironment
{
    private const string ShipOfB = "MobileParty_player_b_0";

    public NavalHandBackScenarioTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void ReturningPlayer_TakesItsAdoptedHullBack_WithItsCurrentConditionAndReserve()
    {
        using var battle = StartServerBattle(atSea: true, out var concluded, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullB = AdoptHullOfB(battle, a, b, c);
        var adopted = a.Hull(hullB);
        adopted.HitPoints = 55f;
        adopted.Frame = new MatrixFrame(Mat3.Identity, new Vec3(120f, 30f, 0f));
        var troopId = CreateRegisteredObject<CharacterObject>();
        a.Call(() =>
        {
            Assert.True(a.Instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            a.RecoveredReserve.AddRange(Enumerable.Range(1, 6).Select(seed => new CoopAgentOrigin(
                troop, null, 0, null, new UniqueTroopDescriptor(seed), battle.PartyIds["B"])));
            // Another party's troop the adopted hull took stays with the host.
            a.RecoveredReserve.Add(new CoopAgentOrigin(troop, null, 0, null, new UniqueTroopDescriptor(99), battle.PartyIds["C"]));
        });
        battle.TickAll(3);
        Assert.Equal(7, adopted.ReservedOrigins.Count);

        var returned = battle.Rejoin(b);
        var fresh = returned.FieldOwnHull(returned.Side, new Vec3(40f, -80f, 0f), battle.PartyIds["B"], ShipOfB);
        // The returning hull has room for four more troops; the other two of its party stay with the host.
        returned.Hull(fresh).CrewCapacity = 4;
        var hero = returned.RegisterAgent(Guid.NewGuid(), "B");
        returned.Call(() => returned.Mission.MainAgent = hero);
        returned.Commit();
        battle.TickAll(3);
        long hostSamplesAtHandBack = a.SentSamples(hullB).Count;

        // The own fresh hull now is the adopted ship, moved to its frame with its condition and the player's queued troops.
        var own = returned.Hull(hullB);
        Assert.Same(returned.Engine.Of(returned.Ship(hullB).Hull), own);
        Assert.Equal("B", returned.Ship(hullB).CurrentAuthority);
        Assert.False(returned.Ship(hullB).IsAdopted);
        Assert.False(returned.Has(fresh));
        Assert.Equal(1, own.Teleports);
        Assert.Equal(0, own.AgentsAboardAtTeleport);
        Assert.Equal(120f, own.Frame.origin.x, 3);
        Assert.Equal(55f, own.HitPoints, 3);
        Assert.Equal(4, own.SpawnableReserve);
        Assert.All(own.ReservedOrigins, origin => Assert.Equal(battle.PartyIds["B"], ((CoopAgentOrigin)origin).MapEventPartyId));
        IReadOnlyList<CoopAgentOrigin> kept = null!;
        a.Call(() => kept = a.Ships.TakeReserveKeptOnHandBack(BattleSideEnum.Attacker));
        Assert.Equal(3, kept.Count);
        Assert.Empty(a.Hull(hullB).ReservedOrigins);
        Assert.Contains(hero, returned.Engine.Boarded);
        Assert.DoesNotContain(returned.Engine.AllHulls, hull => hull.SpawnData?.ShipId == hullB);
        Assert.Empty(returned.Network.NetworkSentMessages.GetMessages<NetworkSpawnBattleShips>()
            .SelectMany(records => records.Ships).Where(record => record.ShipId == fresh));

        // The host released its simulated hull into a copy and follows the returning owner, as the observer does.
        Assert.Equal("B", a.Ship(hullB).CurrentAuthority);
        Assert.True(a.Hull(hullB).IsCopy);
        Assert.Contains(a.Ship(hullB).Hull, a.Engine.Releases);
        Assert.Equal("B", c.Ship(hullB).CurrentAuthority);
        own.Frame = new MatrixFrame(Mat3.Identity, new Vec3(130f, 30f, 0f));
        battle.TickAll(3);
        Assert.Equal(hostSamplesAtHandBack, a.SentSamples(hullB).Count);
        Assert.Equal(130f, a.Hull(hullB).Frame.origin.x, 3);
        Assert.Equal(130f, c.Hull(hullB).Frame.origin.x, 3);
        Assert.Equal("B", returned.SentSamples(hullB).Last().OwnerControllerId);
        Assert.Equal((long)returned.SentSamples(hullB).Last().Sequence, (long)c.Stream(hullB)["accepted"]!);
        Assert.Equal(4, (int)c.Stream(hullB)["reserve"]!);

        ResolveOnHost(a, BattleState.AttackerVictory);
        c.Call(() => c.Controller.ResultCommitter.ReportAcceptedResult(), VictoryConclusionDisabledMethods());
        Assert.Equal(1, concluded());
    }

    // The adopted hull sank before its player came back: the player's launcher fielded it again, but that duplicate goes and
    // its fresh crew withdraws, and the player follows the host's wreck like everyone else.
    [Fact]
    public void ReturningPlayer_WhoseHullSankWhileAdopted_GetsNothingBack()
    {
        using var battle = StartServerBattle(atSea: true, out _, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullB = AdoptHullOfB(battle, a, b, c);
        a.Hull(hullB).HitPoints = 0f;
        a.Hull(hullB).SinkingState = BattleShipCondition.Sunk;
        battle.TickAll(2);

        var returned = battle.Rejoin(b);
        var fresh = returned.FieldOwnHull(returned.Side, new Vec3(40f, -80f, 0f), battle.PartyIds["B"], ShipOfB);
        var freshCrew = returned.RegisterAgent(Guid.NewGuid(), "B");
        returned.Call(() => returned.Engine.PlaceAgent(freshCrew, returned.Ship(fresh).Hull, returned.Ship(fresh).Hull));
        // The returned hero keeps its formation, which can be the duplicate's; it stays in the battle.
        var hero = returned.RegisterAgent(Guid.NewGuid(), "B");
        returned.Call(() =>
        {
            returned.Engine.PlaceAgent(hero, returned.Ship(fresh).Hull, returned.Ship(fresh).Hull);
            returned.Mission.MainAgent = hero;
        });
        var duplicate = returned.Hull(fresh);
        returned.Commit();
        battle.TickAll(4);

        Assert.Empty(returned.Network.NetworkSentMessages.GetMessages<NetworkShipReclaim>());
        Assert.True(duplicate.IsRemoved);
        Assert.Equal(0, duplicate.AgentsAboardAtRemoval);
        Assert.Contains(freshCrew, returned.Engine.Withdrawn);
        Assert.DoesNotContain(hero, returned.Engine.Withdrawn);
        Assert.Contains(returned.Engine.Boardings, boarding =>
            boarding.Hull == null && boarding.Agents.Any(moved => moved.Agent == hero && moved.IsParked));
        Assert.False(returned.Has(fresh));
        Assert.Equal("A", returned.Ship(hullB).CurrentAuthority);
        Assert.True(returned.Hull(hullB).IsCopy);
        Assert.Equal(BattleShipCondition.Sunk, returned.Hull(hullB).SinkingState);
        Assert.Equal("A", c.Ship(hullB).CurrentAuthority);
    }

    // A reclaim repeated after the hand-back, or sent by a player who did not field the hull, changes nothing.
    [Fact]
    public void DuplicateOrForeignReclaim_IsRefused_AndTheHullStaysWhereItIs()
    {
        using var battle = StartServerBattle(atSea: true, out _, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullB = AdoptHullOfB(battle, a, b, c);

        // C asks for B's hull while the host still holds it.
        c.Call(() => c.Network.Send("A", new NetworkShipReclaim(hullB, "C", battle.HostEpoch, 10)));
        battle.TickAll(2);
        Assert.Equal("A", a.Ship(hullB).CurrentAuthority);

        var returned = battle.Rejoin(b);
        returned.FieldOwnHull(returned.Side, new Vec3(40f, -80f, 0f), battle.PartyIds["B"], ShipOfB);
        returned.Commit();
        battle.TickAll(3);
        Assert.Equal("B", a.Ship(hullB).CurrentAuthority);

        // A late repeat of B's own request after the hand-back.
        returned.Call(() => returned.Network.Send("A", new NetworkShipReclaim(hullB, "B", battle.HostEpoch, 10)));
        battle.TickAll(2);

        var refusals = a.Network.NetworkSentMessages.GetMessages<NetworkShipReclaimRefused>().Select(refused => refused.Reason).ToArray();
        Assert.Equal(new[] { "not_original_owner", "not_adopted" }, refusals);
        Assert.Equal(1, a.Network.NetworkSentMessages.GetMessages<NetworkShipHandedBack>().Count());
        Assert.All(battle.Peers, peer => Assert.Equal("B", peer.Ship(hullB).CurrentAuthority));
        JObject handBack = null!;
        returned.Call(() => handBack = JObject.FromObject(returned.Ships.InspectHandBack()));
        Assert.True(1L == (long)handBack["handBacksAccepted"]!, handBack.ToString());
    }

    // B fields its ship, disconnects, and the battle host A adopts and simulates the hull.
    private static Guid AdoptHullOfB(NavalBattle battle, NavalPeer a, NavalPeer b, NavalPeer c)
    {
        battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f), "MobileParty_player_a_0");
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f), ShipOfB);
        battle.FieldPlayerHull(c, new Vec3(80f, 0f, 0f), "MobileParty_player_c_0");
        battle.CommitAll();
        battle.TickAll(2);

        battle.Disconnect(b);
        battle.TickAll(4);
        Assert.False(a.Hull(hullB).IsCopy);
        Assert.Equal("A", c.Ship(hullB).CurrentAuthority);
        return hullB;
    }
}
