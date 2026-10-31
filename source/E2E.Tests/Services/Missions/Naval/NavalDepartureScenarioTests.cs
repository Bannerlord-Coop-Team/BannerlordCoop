using E2E.Tests.Environment.MockEngine;
using Missions.Battles;
using Missions.Messages;
using Newtonsoft.Json.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// M8a, M8c: a player retreats from or leaves a naval battle, and its hulls and crew leave with it on every client. Seats on a
/// departing hull are released before it goes, an ally standing on its deck ends on its own hull, its deck is parked clear a
/// tick before the removal and its crew withdraws from the battle before the hull is removed, so none of it is left parked or
/// active; nothing the departed owner sent late brings it back, and the battle still concludes once.
/// </summary>
public class NavalDepartureScenarioTests : NavalBattleTestEnvironment
{
    public NavalDepartureScenarioTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    // The retreat preset: B presses Tab and Retreat, its mission ends and removes its hull with the removal sent, then the
    // server tells A and C that B left.
    [Fact]
    public void RetreatingPlayer_TakesItsHullOffEveryClient_AndTheBattleStillConcludesOnce()
    {
        using var battle = StartServerBattle(atSea: true, out var concluded, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        var hullC = battle.FieldPlayerHull(c, new Vec3(80f, 0f, 0f));
        battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        var helmsman = RegisterPartyTroopEverywhere(battle, Guid.NewGuid(), "B");
        var ally = RegisterEverywhere(battle, Guid.NewGuid(), "A");
        battle.CommitAll();
        battle.TickAll(2);
        SeatAndBoard(battle, b, hullB, helmsman, ally, hullA);

        Retreat(battle, b);
        battle.TickAll(2);

        foreach (var peer in new[] { a, c })
        {
            AssertRemovedWithClearDeck(peer, hullB, ally[peer], hullA);
            AssertCrewWithdrawnByTheHullRemoval(peer, helmsman[peer]);
            Assert.True(peer.Has(hullA) && peer.Has(hullC));
        }
        Assert.Equal(1, b.Network.NetworkSentMessages.GetMessages<NetworkShipRemoved>()
            .Count(removal => removal.ShipId == hullB && removal.Reason == ShipRemovalReason.Retreat));
        Assert.Equal(1, a.Services.HullLedger.DepartedHulls(BattleSideEnum.Attacker));

        ResolveOnHost(a, BattleState.AttackerVictory);
        c.Call(() => c.Controller.ResultCommitter.ReportAcceptedResult(), VictoryConclusionDisabledMethods());
        Assert.Equal(1, concluded());
        AssertMapEventRemoved(battle.MapEventId);
    }

    // B leaves and its own removal never reaches the others, as when its mesh stops first: the server's departure removes
    // the hull, and everything B sent that is still in flight changes nothing afterwards.
    [Fact]
    public void LeavingPlayer_WhoseRemovalIsLost_LosesItsHullThroughItsDeparture_AndItsLateTrafficIsDropped()
    {
        using var battle = StartServerBattle(atSea: true, out _, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        battle.FieldPlayerHull(c, new Vec3(80f, 0f, 0f));
        var helmsman = RegisterPartyTroopEverywhere(battle, Guid.NewGuid(), "B");
        var ally = RegisterEverywhere(battle, Guid.NewGuid(), "A");
        // B's late crew member reached only B, so its station use waits on A and C.
        var lateCrew = b.RegisterAgent(Guid.NewGuid(), "B");
        battle.CommitAll();
        battle.TickAll(2);
        SeatAndBoard(battle, b, hullB, helmsman, ally, hullA);
        b.UseStation(lateCrew, b.Hull(hullB).Station("oar", 1), inUse: true);
        Assert.Equal(1, (int)c.StationStats()["pending"]!);
        var lateSample = b.SentSamples(hullB).Last();
        var lateRecord = b.Network.NetworkSentMessages.GetMessages<NetworkSpawnBattleShips>().Last();

        battle.Leave(b);
        battle.TickAll(2);
        c.Call(() =>
        {
            c.Broker.Publish(c, lateSample);
            c.Broker.Publish(c, lateRecord);
        });
        battle.TickAll(2);

        foreach (var peer in new[] { a, c })
        {
            AssertRemovedWithClearDeck(peer, hullB, ally[peer], hullA);
            AssertCrewWithdrawnByTheHullRemoval(peer, helmsman[peer]);
            Assert.Equal(0, (int)peer.StationStats()["pending"]!);
            var replicator = JObject.FromObject(peer.Ships.Inspect());
            Assert.Equal(0, (int)replicator["pendingForeignHulls"]!);
            Assert.Equal(0, (int)replicator["heldSamples"]!);
        }
        Assert.Single(c.Engine.AllHulls, hull => hull.SpawnData?.ShipId == hullB);
    }

    // D4: B's connection drops without a leave. The server tells A and C it left, as it sends every naval disconnect: its hull
    // goes with a clear deck and its crew withdraws, its late traffic changes nothing, and the host keeps simulating the AI fleet.
    [Fact]
    public void DisconnectedPlayer_LosesItsHullOnEveryPeer_AndTheHostKeepsFighting()
    {
        using var battle = StartServerBattle(atSea: true, out var concluded, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        battle.FieldPlayerHull(c, new Vec3(80f, 0f, 0f));
        var pirate = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        var helmsman = RegisterPartyTroopEverywhere(battle, Guid.NewGuid(), "B");
        var ally = RegisterEverywhere(battle, Guid.NewGuid(), "A");
        battle.CommitAll();
        battle.TickAll(2);
        SeatAndBoard(battle, b, hullB, helmsman, ally, hullA);
        var lateSample = b.SentSamples(hullB).Last();

        DropConnection(b);
        battle.TickAll(2);
        c.Call(() => c.Broker.Publish(c, lateSample));
        long pirateStreamed = (long)c.Stream(pirate)["accepted"]!;
        a.Hull(pirate).Frame = new MatrixFrame(Mat3.Identity, new Vec3(12f, 300f, 0f));
        battle.TickAll(2);

        foreach (var peer in new[] { a, c })
        {
            Assert.Single(peer.Instance.InternalMessages.GetMessages<MissionPeerLeft>(), left => left.ControllerId == "B");
            Assert.Empty(peer.Instance.InternalMessages.GetMessages<MissionPeerDisconnected>());
            AssertRemovedWithClearDeck(peer, hullB, ally[peer], hullA);
            AssertCrewWithdrawnByTheHullRemoval(peer, helmsman[peer]);
            Assert.False(peer.Has(hullB));
            Assert.Equal("A", peer.Ship(pirate).CurrentAuthority);
        }
        Assert.Equal(1, a.Services.HullLedger.DepartedHulls(BattleSideEnum.Attacker));
        Assert.False(a.Hull(pirate).IsCopy);
        Assert.True((long)c.Stream(pirate)["accepted"]! > pirateStreamed);
        Assert.Equal(12f, c.Hull(pirate).Frame.origin.x, 3);

        ResolveOnHost(a, BattleState.AttackerVictory);
        c.Call(() => c.Controller.ResultCommitter.ReportAcceptedResult(), VictoryConclusionDisabledMethods());
        Assert.Equal(1, concluded());
        AssertMapEventRemoved(battle.MapEventId);
    }

    // D4: the battle host drops. Its own hull leaves every client like any disconnected player's, while the AI hulls it
    // simulated move to the promoted successor, which swaps them for simulated hulls and streams them at the new epoch.
    [Fact]
    public void DisconnectedHost_LosesItsOwnHull_AndItsSuccessorTakesOverTheAiHulls()
    {
        using var battle = StartNavalBattle(new[]
        {
            ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker), ("C", BattleSideEnum.Attacker),
        });
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var pirate = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        var crewA = RegisterPartyTroopEverywhere(battle, Guid.NewGuid(), "A");
        battle.CommitAll();
        battle.TickAll(2);
        foreach (var peer in battle.Peers)
            peer.Engine.PlaceAgent(crewA[peer], peer.Ship(hullA).Hull, peer.Ship(hullA).Hull);

        battle.MigrateHost(a, "B");
        // The successor adopts the AI A ran, never A's own party, which waits for its hull's removal.
        string? crewAuthorityOnSuccessor = null;
        b.Call(() => crewAuthorityOnSuccessor = b.Agents.TryGetAgentInfo(crewA[b], out var info) ? info.CurrentAuthority : null);
        battle.TickAll(3);

        Assert.Equal("A", crewAuthorityOnSuccessor);
        Assert.All(new[] { b, c }, peer =>
        {
            AssertRemovedWithClearDeck(peer, hullA);
            AssertCrewWithdrawnByTheHullRemoval(peer, crewA[peer]);
            Assert.Equal("B", peer.Ship(pirate).CurrentAuthority);
        });
        Assert.False(b.Hull(pirate).IsCopy);
        b.Hull(pirate).Frame = new MatrixFrame(Mat3.Identity, new Vec3(-20f, 300f, 0f));
        b.Tick();
        c.Tick();
        Assert.Equal(battle.HostEpoch, b.SentSamples(pirate).Last().HostEpoch);
        Assert.Equal(-20f, c.Hull(pirate).Frame.origin.x, 3);
    }

    // R41a-2: B's troop stands on A's deck, off every hull of B's. The hull removal can't take it, so the party withdrawal
    // fades it, but only once B's hull is gone: until then nothing of B's party leaves the battle outside the hull path.
    [Fact]
    public void DisconnectedPlayer_TroopOffItsHulls_FadesOnlyAfterItsHullRemovalFinished()
    {
        using var battle = StartServerBattle(atSea: true, out _, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        battle.FieldPlayerHull(c, new Vec3(80f, 0f, 0f));
        var helmsman = RegisterPartyTroopEverywhere(battle, Guid.NewGuid(), "B");
        var straggler = RegisterPartyTroopEverywhere(battle, Guid.NewGuid(), "B");
        var ally = RegisterEverywhere(battle, Guid.NewGuid(), "A");
        battle.CommitAll();
        battle.TickAll(2);
        SeatAndBoard(battle, b, hullB, helmsman, ally, hullA);
        foreach (var peer in battle.Peers)
            peer.Engine.PlaceAgent(straggler[peer], peer.Ship(hullA).Hull);

        DropConnection(b);
        var survivors = new[] { a, c };
        foreach (var peer in survivors) peer.Tick();
        Assert.All(survivors, peer => Assert.True(straggler[peer].IsActive(), $"{peer.Id} faded B's party while parking its hull"));
        foreach (var peer in survivors) peer.Tick();
        Assert.All(survivors, peer =>
        {
            Assert.True(CopyOf(peer, hullB).IsRemoved);
            Assert.True(straggler[peer].IsActive(), $"{peer.Id} faded B's party in the tick that removed its hull");
        });
        foreach (var peer in survivors) peer.Tick();

        foreach (var peer in survivors)
        {
            AssertRemovedWithClearDeck(peer, hullB, ally[peer], hullA);
            AssertCrewWithdrawnByTheHullRemoval(peer, helmsman[peer]);
            Assert.False(straggler[peer].IsActive(), $"{peer.Id} kept B's troop off its hull in the battle");
            Assert.DoesNotContain(straggler[peer], peer.Engine.Withdrawn);
            bool registered = true;
            peer.Call(() => registered = peer.Agents.TryGetAgentInfo(straggler[peer], out _));
            Assert.False(registered, $"{peer.Id} kept B's troop off its hull registered");
        }
    }

    // R41a-2: B's own removal reached the survivors and they parked its hull, then the server's departure came a tick later.
    // The party withdrawal waits for that removal in flight, so the hull path still takes B's crew.
    [Fact]
    public void LeavingPlayer_DepartureATickAfterItsRemoval_WaitsForTheRemovalInFlight()
    {
        using var battle = StartServerBattle(atSea: true, out _, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        battle.FieldPlayerHull(c, new Vec3(80f, 0f, 0f));
        var helmsman = RegisterPartyTroopEverywhere(battle, Guid.NewGuid(), "B");
        var straggler = RegisterPartyTroopEverywhere(battle, Guid.NewGuid(), "B");
        var ally = RegisterEverywhere(battle, Guid.NewGuid(), "A");
        battle.CommitAll();
        battle.TickAll(2);
        SeatAndBoard(battle, b, hullB, helmsman, ally, hullA);
        foreach (var peer in battle.Peers)
            peer.Engine.PlaceAgent(straggler[peer], peer.Ship(hullA).Hull);
        var survivors = new[] { a, c };

        EndMission(b, ShipRemovalReason.Leave);
        foreach (var peer in survivors) peer.Tick();
        Assert.All(survivors, peer => Assert.Equal(1, CopyOf(peer, hullB).Parkings));
        battle.Leave(b);
        foreach (var peer in survivors) peer.Tick();
        Assert.All(survivors, peer =>
        {
            Assert.True(CopyOf(peer, hullB).IsRemoved);
            Assert.True(straggler[peer].IsActive(), $"{peer.Id} faded B's party before its removal in flight finished");
        });
        foreach (var peer in survivors) peer.Tick();

        foreach (var peer in survivors)
        {
            AssertRemovedWithClearDeck(peer, hullB, ally[peer], hullA);
            AssertCrewWithdrawnByTheHullRemoval(peer, helmsman[peer]);
            Assert.False(straggler[peer].IsActive(), $"{peer.Id} kept B's troop off its hull in the battle");
        }
    }

    // R41a-2 C1: B leaves and enters again before its old hull settled, as a late joiner still loading gets both back to back.
    // The party withdrawal selects by party, so it must go with the re-entry or it would fade B's fresh troops.
    [Fact]
    public void LeavingPlayer_ReEnteringBeforeItsHullSettled_KeepsItsFreshTroops()
    {
        using var battle = StartServerBattle(atSea: true, out _, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        battle.FieldPlayerHull(c, new Vec3(80f, 0f, 0f));
        battle.CommitAll();
        battle.TickAll(2);
        var survivors = new[] { a, c };

        battle.Leave(b);
        foreach (var peer in survivors) peer.Tick();
        Assert.All(survivors, peer => Assert.Equal(1, CopyOf(peer, hullB).Parkings));
        battle.AnnounceEntry(b);
        var fresh = survivors.ToDictionary(peer => peer, peer => peer.RegisterPartyTroop(Guid.NewGuid(), "B"));
        foreach (var peer in survivors)
            peer.Engine.PlaceAgent(fresh[peer], peer.Ship(hullA).Hull);
        foreach (var peer in survivors) peer.Ticks(3);

        foreach (var peer in survivors)
        {
            Assert.True(CopyOf(peer, hullB).IsRemoved);
            Assert.True(fresh[peer].IsActive(), $"{peer.Id} faded B's fresh troop with its old party");
            bool registered = false;
            peer.Call(() => registered = peer.Agents.TryGetAgentInfo(fresh[peer], out _));
            Assert.True(registered, $"{peer.Id} deregistered B's fresh troop with its old party");
        }
    }

    // R41a fix-loop C3: the host and a player drop together. The successor is mid-swap on the AI hull it took over when the
    // player's hull leaves, so an AI boarder on that deck goes to a friendly hull that stays, never onto the hull the swap
    // replaces next tick (the R33, R40 crash class).
    [Fact]
    public void PlayerLeavesWhileTheSuccessorSwapsAnAiHull_ItsAiBoarderGoesToAHullThatStays()
    {
        using var battle = StartNavalBattle(new[]
        {
            ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker), ("C", BattleSideEnum.Attacker),
        });
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        var hullC = battle.FieldPlayerHull(c, new Vec3(80f, 0f, 0f));
        var allied = battle.FieldAiHull(a, BattleSideEnum.Attacker, new Vec3(60f, 20f, 0f));
        var boarder = RegisterEverywhere(battle, Guid.NewGuid(), "A");
        battle.CommitAll();
        battle.TickAll(2);
        foreach (var peer in battle.Peers)
            peer.Engine.PlaceAgent(boarder[peer], peer.Ship(hullC).Hull, peer.Ship(allied).Hull);

        battle.MigrateHost(a, "B");
        b.Tick();
        Assert.Equal("ReplaceHull", (string)b.Stream(allied)["swapPhase"]!);
        battle.Leave(c);
        battle.TickAll(3);

        Assert.Null((string?)b.Stream(allied)["swapPhase"]);
        Assert.False(b.Hull(allied).IsCopy);
        Assert.Same(b.Ship(hullB).Hull, b.Engine.DeckOf(boarder[b])!.Hull);
        Assert.True(boarder[b].IsActive());
        AssertRemovedWithClearDeck(b, hullC);
    }

    // D4: a player that disconnected comes back as a late joiner with a fresh hull, which every member spawns as a new copy;
    // its old hull never comes back.
    [Fact]
    public void DisconnectedPlayer_ReturnsAsALateJoiner_WithAFreshHull()
    {
        using var battle = StartServerBattle(atSea: true, out _, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var oldHullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        battle.CommitAll();
        battle.TickAll(2);
        battle.Disconnect(b);
        battle.TickAll(2);

        var returned = battle.Rejoin(b);
        var freshHullB = battle.FieldPlayerHull(returned, new Vec3(45f, 0f, 0f));
        returned.Commit();
        battle.TickAll(3);

        foreach (var peer in new[] { a, c })
        {
            Assert.True(peer.Has(freshHullB), $"{peer.Id} never spawned the returning player's fresh hull");
            Assert.Equal("B", peer.Ship(freshHullB).CurrentAuthority);
            Assert.True(peer.Hull(freshHullB).IsCopy);
            Assert.False(peer.Has(oldHullB));
        }
        Assert.Equal(3, returned.Registry.Ships.Count);
        Assert.False(returned.Has(oldHullB));
    }

    // A per-ship Retreat order: B's hull sails off the map and vanilla's ShipRetreatLogic on B keeps the crew off its deck,
    // then removes it, fading the crew aboard, which reaches the others as routs. They withdraw nobody with the hull, so the
    // crew aboard goes through its rout and the crew off the deck stays. B stays in the battle with its other hull.
    [Fact]
    public void HullRetreatedMidBattle_LeavesEveryClient_ItsCrewAboardRouts_AndItsCrewOffTheDeckAndOtherHullStay()
    {
        using var battle = StartServerBattle(atSea: true, out var concluded, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var retreating = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        var staying = battle.FieldPlayerHull(b, new Vec3(60f, 0f, 0f));
        var ally = RegisterEverywhere(battle, Guid.NewGuid(), "A");
        var retreatingCrewId = Guid.NewGuid();
        var retreatingCrew = RegisterEverywhere(battle, retreatingCrewId, "B");
        var boarder = RegisterEverywhere(battle, Guid.NewGuid(), "B");
        battle.CommitAll();
        battle.TickAll(2);
        foreach (var peer in battle.Peers)
        {
            peer.Engine.PlaceAgent(ally[peer], peer.Ship(retreating).Hull, peer.Ship(hullA).Hull);
            peer.Engine.PlaceAgent(retreatingCrew[peer], peer.Ship(retreating).Hull, peer.Ship(retreating).Hull);
            peer.Engine.PlaceAgent(boarder[peer], peer.Ship(staying).Hull, peer.Ship(retreating).Hull);
        }

        b.Call(() =>
        {
            var hull = b.Ship(retreating).Hull;
            b.Ships.AnnounceRemoval(hull, ShipRemovalReason.Retreat, missionEnding: false);
            // Stands for vanilla ShipRetreatLogic removing B's own hull.
            b.Engine.VanillaOwnerRemoveShipForTest(hull);
        });
        foreach (var peer in new[] { a, c })
            peer.Call(() => peer.Broker.Publish(peer, new NetworkBattleAgentRouted(retreatingCrewId)));
        battle.TickAll(2);
        long streamedBefore = (long)c.Stream(staying)["accepted"]!;
        battle.TickAll(2);

        foreach (var peer in new[] { a, c })
        {
            AssertRemovedWithClearDeck(peer, retreating, ally[peer], hullA, crewKept: 1);
            AssertCrewWithdrawn(peer, retreatingCrew[peer]);
            Assert.True(boarder[peer].IsActive(), $"{peer.Id} took the crew off the retreating hull's deck out of the battle");
            Assert.Same(peer.Ship(staying).Hull, peer.Engine.DeckOf(boarder[peer])!.Hull);
            bool registered = false;
            peer.Call(() => registered = peer.Agents.TryGetAgentInfo(boarder[peer], out _));
            Assert.True(registered, $"{peer.Id} dropped the crew off the retreating hull's deck from the registry");
            Assert.Empty(peer.Engine.Withdrawn);
        }
        Assert.Same(b.Ship(hullA).Hull, b.Engine.DeckOf(ally[b])!.Hull);
        Assert.All(battle.Peers, peer => Assert.True(peer.Has(staying)));
        Assert.True((long)c.Stream(staying)["accepted"]! > streamedBefore);
        Assert.All(battle.Peers, peer => Assert.Equal(1, peer.Services.HullLedger.DepartedHulls(BattleSideEnum.Attacker)));
        Assert.Equal(0, concluded());
    }

    // R40 crash A: vs_pirates fought to the end, the result held on every client, then the battle host leaves through the
    // results screen. Its removal reaches the others over the mesh well before the server's departure notice, which the
    // crashed client never got to: the host's crew must already be out of the battle when its hull is removed.
    [Fact]
    public void ConcludedBattle_HostLeavesFirst_EverySurvivorWithdrawsItsCrewBeforeRemovingItsHull()
    {
        using var battle = StartServerBattle(atSea: true, out _, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        battle.FieldPlayerHull(c, new Vec3(80f, 0f, 0f));
        var helmsman = RegisterEverywhere(battle, Guid.NewGuid(), "A");
        var deckhand = RegisterEverywhere(battle, Guid.NewGuid(), "A");
        var ally = RegisterEverywhere(battle, Guid.NewGuid(), "B");
        battle.CommitAll();
        battle.TickAll(2);
        SeatAndBoard(battle, a, hullA, helmsman, ally, hullB);
        foreach (var peer in battle.Peers)
            peer.Engine.PlaceAgent(deckhand[peer], peer.Ship(hullA).Hull, peer.Ship(hullA).Hull);
        ConcludeEverywhere(battle, a);

        LeaveConcludedBattle(battle, a, new[] { b, c }, new[] { hullA });

        foreach (var peer in new[] { b, c })
        {
            AssertRemovedWithClearDeck(peer, hullA, ally[peer], hullB);
            AssertCrewWithdrawn(peer, helmsman[peer], deckhand[peer]);
        }
    }

    // R40 crash B2: the fleet preset fought to the end, then peer B leaves with its three hulls while the host survives.
    [Fact]
    public void ConcludedBattle_PeerWithThreeHullsLeavesFirst_EverySurvivorWithdrawsTheirCrewBeforeRemovingThem()
    {
        using var battle = StartServerBattle(atSea: true, out _, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var fleetB = new[]
        {
            battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f)),
            battle.FieldPlayerHull(b, new Vec3(60f, 0f, 0f)),
            battle.FieldPlayerHull(b, new Vec3(80f, 0f, 0f)),
        };
        battle.FieldPlayerHull(c, new Vec3(120f, 0f, 0f));
        var crews = fleetB.Select(_ => RegisterEverywhere(battle, Guid.NewGuid(), "B")).ToArray();
        var helmsman = RegisterEverywhere(battle, Guid.NewGuid(), "B");
        var ally = RegisterEverywhere(battle, Guid.NewGuid(), "A");
        battle.CommitAll();
        battle.TickAll(2);
        SeatAndBoard(battle, b, fleetB[0], helmsman, ally, hullA);
        foreach (var peer in battle.Peers)
            for (int i = 0; i < fleetB.Length; i++)
                peer.Engine.PlaceAgent(crews[i][peer], peer.Ship(fleetB[i]).Hull, peer.Ship(fleetB[i]).Hull);
        ConcludeEverywhere(battle, a);

        LeaveConcludedBattle(battle, b, new[] { a, c }, fleetB);

        foreach (var peer in new[] { a, c })
        {
            AssertRemovedWithClearDeck(peer, fleetB[0], ally[peer], hullA);
            foreach (var hull in fleetB.Skip(1))
                AssertRemovedWithClearDeck(peer, hull);
            AssertCrewWithdrawn(peer, crews.Select(crew => crew[peer]).Append(helmsman[peer]).ToArray());
        }
    }

    // The host's end logic resolves the battle and every client holds the result, as when the results screen shows.
    private void ConcludeEverywhere(NavalBattle battle, NavalPeer host)
    {
        ResolveOnHost(host, BattleState.AttackerVictory);
        foreach (var peer in battle.Peers)
            Assert.True(peer.Controller.ResultCommitter.TryGetResolvedState(out _), $"{peer.Id} holds no result");
    }

    // The live order of R40: the leaver's mission ends and its removals reach the survivors over the mesh, whose next tick
    // parks the departing decks and the one after withdraws the crew and removes the hulls; only then does the server's
    // departure notice arrive, and it finds nothing left to withdraw.
    private static void LeaveConcludedBattle(NavalBattle battle, NavalPeer leaver, NavalPeer[] survivors, Guid[] leaverHulls)
    {
        EndMission(leaver, ShipRemovalReason.Leave);

        foreach (var peer in survivors)
            peer.Tick();
        foreach (var peer in survivors)
        {
            foreach (var shipId in leaverHulls)
            {
                var copy = CopyOf(peer, shipId);
                Assert.Equal(1, copy.Parkings);
                Assert.False(copy.IsRemoved, $"{peer.Id} removed {shipId} in the tick that parked it");
            }
        }

        foreach (var peer in survivors)
            peer.Tick();
        foreach (var peer in survivors)
            foreach (var shipId in leaverHulls)
                Assert.True(CopyOf(peer, shipId).IsRemoved, $"{peer.Id} kept {shipId} past the tick after its parking");

        battle.Leave(leaver);
        battle.TickAll(2);
    }

    private static FakeHull CopyOf(NavalPeer peer, Guid shipId) =>
        peer.Engine.AllHulls.Single(hull => hull.SpawnData?.ShipId == shipId);

    // A departing hull's crew is out of the battle and its registry, and nothing of it is left parked.
    private static void AssertCrewWithdrawn(NavalPeer peer, params Agent[] crew)
    {
        for (int i = 0; i < crew.Length; i++)
        {
            var agent = crew[i];
            Assert.False(agent.IsActive(), $"{peer.Id} kept departed crew member {i} in the battle");
            Assert.DoesNotContain(agent, peer.Engine.ParkedAgents);
            Assert.Null(peer.Engine.DeckOf(agent));
            bool registered = true;
            peer.Call(() => registered = peer.Agents.TryGetAgentInfo(agent, out _));
            Assert.False(registered, $"{peer.Id} kept departed crew member {i} registered");
        }

        Assert.Empty(peer.Engine.ParkedAgents);
    }

    // R41a-2: the leaver's party fade ran before the hull removal, which then parked and withdrew nobody. The hull removal
    // must take its crew, so the party withdrawal finds only what was left off its hulls.
    private static void AssertCrewWithdrawnByTheHullRemoval(NavalPeer peer, params Agent[] crew)
    {
        AssertCrewWithdrawn(peer, crew);
        foreach (var agent in crew)
            Assert.Contains(agent, peer.Engine.Withdrawn);
    }

    private static Dictionary<NavalPeer, Agent> RegisterEverywhere(NavalBattle battle, Guid agentId, string authority) =>
        battle.Peers.ToDictionary(peer => peer, peer => peer.RegisterAgent(agentId, authority));

    // A troop of the owner's own party on every client, which withdraws with the party when the owner leaves.
    private static Dictionary<NavalPeer, Agent> RegisterPartyTroopEverywhere(NavalBattle battle, Guid agentId, string owner) =>
        battle.Peers.ToDictionary(peer => peer, peer => peer.RegisterPartyTroop(agentId, owner));

    // B's helmsman, crew of B's hull, takes the helm, which seats its puppet on A and C, and A's ally boards B's deck on every
    // client.
    private static void SeatAndBoard(NavalBattle battle, NavalPeer owner, Guid hull, Dictionary<NavalPeer, Agent> helmsman,
        Dictionary<NavalPeer, Agent> ally, Guid allyHull)
    {
        owner.UseStation(helmsman[owner], owner.Hull(hull).Station("helm"), inUse: true);
        foreach (var peer in battle.Peers)
        {
            peer.Engine.PlaceAgent(helmsman[peer], peer.Ship(hull).Hull, peer.Ship(hull).Hull);
            peer.Engine.PlaceAgent(ally[peer], peer.Ship(hull).Hull, peer.Ship(allyHull).Hull);
            if (peer != owner) Assert.Same(helmsman[peer], peer.Hull(hull).SeatedAt("helm"));
        }
    }

    // Vanilla's retreat on the leaver: OnRetreatMission, then its mission ends, then the mission leave reaches the server,
    // which tells every other member.
    private static void Retreat(NavalBattle battle, NavalPeer leaver)
    {
        EndMission(leaver, ShipRemovalReason.Retreat);
        battle.Leave(leaver);
    }

    // NavalShipsLogic.OnEndMission on the leaver sends the final ropes and removes and announces its own hulls. Its copies are
    // left out: they go with its mission, which nothing reads again, and announcing one sends nothing.
    private static void EndMission(NavalPeer leaver, ShipRemovalReason reason) => leaver.Call(() =>
    {
        leaver.Ships.SendFinalRopes();
        foreach (var ship in leaver.Registry.Ships.Where(ship => ship.CurrentAuthority == leaver.Id).ToList())
        {
            leaver.Ships.AnnounceRemoval(ship.Hull, reason, missionEnding: true);
            // Stands for vanilla NavalShipsLogic.OnEndMission removing the leaver's own hull.
            leaver.Engine.VanillaOwnerRemoveShipForTest(ship.Hull);
        }
    });

    // Parked a tick before it went, with nobody aboard or seated and none of its crew but the kept ones in the battle at the
    // removal.
    private static void AssertRemovedWithClearDeck(NavalPeer peer, Guid departed, int crewKept = 0)
    {
        ShipLifecycle state = ShipLifecycle.Unknown;
        peer.Call(() => state = peer.Registry.StateOf(departed));
        Assert.Equal(ShipLifecycle.Removed, state);

        var copy = CopyOf(peer, departed);
        Assert.True(copy.IsRemoved, $"{peer.Id} kept the copy of {departed}");
        Assert.Equal(1, copy.ParkingsBeforeRemoval);
        Assert.Equal(crewKept, copy.CrewActiveAtRemoval);
        Assert.Empty(copy.Seats);
    }

    private static void AssertRemovedWithClearDeck(NavalPeer peer, Guid departed, Agent ally, Guid allyHull, int crewKept = 0)
    {
        AssertRemovedWithClearDeck(peer, departed, crewKept);
        Assert.Same(peer.Ship(allyHull).Hull, peer.Engine.DeckOf(ally)!.Hull);
    }
}
