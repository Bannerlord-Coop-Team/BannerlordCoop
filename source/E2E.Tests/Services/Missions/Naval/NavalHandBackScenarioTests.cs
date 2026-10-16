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
        // B's helmsman takes the fresh hull's helm during deployment, so its seat goes out under the fresh ship id.
        var helmsmanId = Guid.NewGuid();
        var helmsman = battle.Peers.ToDictionary(peer => peer, peer => peer.RegisterAgent(helmsmanId, "B"));
        returned.UseStation(helmsman[returned], returned.Hull(fresh).Station("helm"), inUse: true);
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
        Assert.Same(helmsman[a], a.Hull(hullB).SeatedAt("helm"));
        Assert.Same(helmsman[c], c.Hull(hullB).SeatedAt("helm"));
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

    // M8c r1: the host's records reach B only after B committed, so the others spawn a copy of B's fresh hull before its
    // adopted ship is matched, and the hand-back tells them to drop that copy. B keeps the hull and its whole crew under the
    // adopted ship id, so the crew and hero they had on the copy stay registered and in the battle.
    [Fact]
    public void ReturningPlayer_WhoseFreshHullWasAnnounced_KeepsItsCrewAndHeroOnEveryClient()
    {
        using var battle = StartServerBattle(atSea: true, out _, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullB = AdoptHullOfB(battle, a, b, c);

        b.Call(() =>
        {
            b.DisposeShipServices();
            b.Controller.Dispose();
        });
        var returned = battle.Join(b.Id);
        var fresh = returned.FieldOwnHull(returned.Side, new Vec3(40f, -80f, 0f), battle.PartyIds["B"], ShipOfB);
        a.Network.RouteMessages = false;
        battle.AnnounceEntry(returned);
        a.Network.RouteMessages = true;
        var hostRecords = a.Network.NetworkSentMessages.GetMessages<NetworkSpawnBattleShips>().Last();
        Assert.Contains(hostRecords.Ships, record => record.ShipId == hullB);
        returned.Commit();
        returned.Tick();
        a.Tick();
        c.Tick();
        Assert.All(new[] { a, c }, peer => Assert.True(peer.Has(fresh), $"{peer.Id} spawned no copy of the fresh hull"));
        Assert.All(new[] { a, c }, peer => Assert.Equal("B", peer.Ship(fresh).CurrentAuthority));

        var crewId = Guid.NewGuid();
        var heroId = Guid.NewGuid();
        var crew = battle.Peers.ToDictionary(peer => peer, peer => peer.RegisterAgent(crewId, "B"));
        var hero = battle.Peers.ToDictionary(peer => peer, peer => peer.RegisterAgent(heroId, "B"));
        returned.Call(() => returned.Mission.MainAgent = hero[returned]);
        foreach (var peer in battle.Peers)
        {
            var freshHull = peer.Ship(fresh).Hull;
            peer.Engine.PlaceAgent(crew[peer], freshHull, freshHull);
            peer.Engine.PlaceAgent(hero[peer], freshHull, freshHull);
        }

        returned.Call(() => returned.Broker.Publish(returned, hostRecords));
        battle.TickAll(4);

        Assert.Equal("B", returned.Ship(hullB).CurrentAuthority);
        Assert.Contains(returned.Network.NetworkSentMessages.GetMessages<NetworkShipRemoved>(),
            removal => removal.ShipId == fresh && removal.Reason == ShipRemovalReason.Reclaimed && !removal.MissionEnding);
        foreach (var peer in new[] { a, c })
        {
            Assert.False(peer.Has(fresh), $"{peer.Id} kept the copy of the fresh hull");
            Assert.True(peer.Engine.AllHulls.Single(hull => hull.SpawnData?.ShipId == fresh).IsRemoved);
            Assert.Equal("B", peer.Ship(hullB).CurrentAuthority);
            foreach (var agent in new[] { crew[peer], hero[peer] })
            {
                Assert.True(agent.IsActive(), $"{peer.Id} took the returning player's agent out of the battle");
                bool registered = false;
                peer.Call(() => registered = peer.Agents.TryGetAgentInfo(agent, out _));
                Assert.True(registered, $"{peer.Id} dropped the returning player's agent from the registry");
            }

            Assert.Empty(peer.Engine.Withdrawn);
            Assert.Empty(peer.Engine.ParkedAgents);
        }
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

    // The host retreats the adopted hull while its player is still deploying: the ship left the battle, so the player's
    // duplicate goes once deployment is over instead of staying hidden from peers.
    [Fact]
    public void ReturningPlayer_WhoseAdoptedHullLeftDuringItsDeployment_RemovesItsDuplicate()
    {
        using var battle = StartServerBattle(atSea: true, out _, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullB = AdoptHullOfB(battle, a, b, c);
        var returned = battle.Rejoin(b);
        var fresh = returned.FieldOwnHull(returned.Side, new Vec3(40f, -80f, 0f), battle.PartyIds["B"], ShipOfB);
        var duplicate = returned.Hull(fresh);
        battle.TickAll(2);
        Assert.Single(Reclaims(returned));

        a.Call(() =>
        {
            var hull = a.Ship(hullB).Hull;
            a.Ships.AnnounceRemoval(hull, ShipRemovalReason.Retreat, missionEnding: false);
            a.Engine.RemoveHull(hull);
        });
        battle.TickAll(2);
        returned.Commit();
        battle.TickAll(3);

        Assert.Empty(Reclaims(returned));
        Assert.True(duplicate.IsRemoved);
        Assert.False(returned.Has(fresh));
        Assert.Empty(returned.Network.NetworkSentMessages.GetMessages<NetworkShipReclaim>());
        Assert.All(battle.Peers, peer => Assert.False(peer.Has(hullB)));
        Assert.All(new[] { a, c }, peer => Assert.False(peer.Has(fresh)));
    }

    private static JArray Reclaims(NavalPeer peer)
    {
        JObject handBack = null!;
        peer.Call(() => handBack = JObject.FromObject(peer.Ships.InspectHandBack()));
        return (JArray)handBack["reclaims"]!;
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
