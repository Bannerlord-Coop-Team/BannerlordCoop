using Missions.Messages;
using Newtonsoft.Json.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// In-mission ship capture (M6 capture plan): the captor's elapsed helm timer goes to the captured hull's authority, which
/// decides on its real crew and broadcasts the capture; every client replays vanilla's swap and the prize moves to its captor
/// in place. H is the battle host, C the captor and O an observer, all on the attacking side against an AI fleet.
/// </summary>
public class NavalCaptureScenarioTests : NavalBattleTestEnvironment
{
    public NavalCaptureScenarioTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    // (a) C captures an AI hull H simulates: C owns it, H and O keep a copy of C's, the streams and damage follow C.
    [Fact]
    public void CaptorCapturesAnAiHull_OwnsItInPlace_AndItsStreamsAndDamageFollowTheCaptor()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out var o, out var pirates, out var captorHull, out _);
        var prize = pirates[0];
        var lateSampleFromH = h.SentSamples(prize).Last();
        h.Hull(prize).IsVacant = true;

        c.ElapseCaptureTimer(prize, captorHull);

        foreach (var peer in new[] { h, c, o })
        {
            var ship = peer.Ship(prize);
            Assert.Equal("C", ship.CurrentAuthority);
            Assert.Equal("C", ship.CaptorControllerId);
            Assert.False(ship.FollowsBattleHost);
            Assert.True(peer.Ship(captorHull).IsOrphaned);
            Assert.Equal(BattleSideEnum.Attacker, peer.Hull(prize).Side);
            Assert.Equal(BattleSideEnum.None, peer.Hull(captorHull).Side);
            Assert.Equal(1L, (long)CaptureStats(peer)["applied"]!["capturesApplied"]!);
            Assert.Equal(1, peer.Ships.DepartedHulls(BattleSideEnum.Defender));
        }

        Assert.False(c.Hull(prize).IsCopy);
        Assert.Contains(c.Ship(prize).Hull, c.Engine.Adoptions);
        Assert.True(h.Hull(prize).IsCopy);
        Assert.Contains(h.Ship(prize).Hull, h.Engine.Releases);
        Assert.True(o.Hull(prize).IsCopy);
        Assert.Empty(o.Engine.Adoptions);

        // The captor streams the prize from its own count; the old authority's late sample is no longer its.
        c.Hull(prize).Frame = new MatrixFrame(Mat3.Identity, new Vec3(12f, 250f, 0f));
        battle.TickAll(2);
        Assert.Equal(12f, h.Hull(prize).Frame.origin.x, 3);
        Assert.Equal(12f, o.Hull(prize).Frame.origin.x, 3);
        o.Call(() => o.Instance.SimulateMessage(h.Network.NetPeer, lateSampleFromH));
        Assert.Equal("not_authority", (string)o.Stream(prize)["lastReject"]!);

        // A hit on the prize goes to the captor, and every client ends at its HP.
        o.Hit(prize, 30f);
        battle.TickAll(2);
        Assert.Single(c.Hull(prize).AppliedDamage);
        Assert.Empty(h.Hull(prize).AppliedDamage);
        Assert.All(new[] { h, c, o }, peer => Assert.Equal(70f, peer.Hull(prize).HitPoints, 3));
    }

    // (b) The battle host captures an AI hull it simulates: nothing moves, but the prize stops following the host, so when H
    // leaves the prize and its orphan go with it while the other AI hull moves to the successor.
    [Fact]
    public void HostCapturesItsOwnAiHull_ThePrizeAndOrphanLeaveWithTheHost_AndTheRestMoveToTheSuccessor()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out var o, out var pirates, out _, out var hostHull);
        var prize = pirates[0];
        h.Hull(prize).IsVacant = true;

        h.ElapseCaptureTimer(prize, hostHull);

        foreach (var peer in new[] { h, c, o })
        {
            Assert.Equal("H", peer.Ship(prize).CurrentAuthority);
            Assert.Equal("H", peer.Ship(prize).CaptorControllerId);
            Assert.False(peer.Ship(prize).FollowsBattleHost);
            Assert.True(peer.Ship(hostHull).IsOrphaned);
        }
        Assert.Empty(h.Engine.Adoptions);
        Assert.Empty(h.Engine.Releases);
        Assert.Empty(h.Network.NetworkSentMessages.GetMessages<NetworkShipCaptureRequest>());

        battle.MigrateHost(h, "C");
        battle.TickAll(4);

        foreach (var peer in new[] { c, o })
        {
            Assert.False(peer.Has(prize), $"{peer.Id} kept the host's prize");
            Assert.False(peer.Has(hostHull), $"{peer.Id} kept the host's orphan");
            Assert.Equal("C", peer.Ship(pirates[1]).CurrentAuthority);
        }
        Assert.False(c.Hull(pirates[1]).IsCopy);
    }

    // (c) PvP: the victim's client is the authority of its own hull and confirms the capture; the prize changes sides.
    [Fact]
    public void PlayerCapturesAnotherPlayersHull_TheVictimConfirms_AndThePrizeChangesSides()
    {
        using var battle = StartNavalBattle(new[]
        {
            ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Defender), ("O", BattleSideEnum.Attacker),
        });
        var (a, b, o) = (battle["A"], battle["B"], battle["O"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f), formationIndex: 0);
        var hullB = battle.FieldPlayerHull(b, new Vec3(0f, 40f, 0f), formationIndex: 0);
        battle.FieldPlayerHull(o, new Vec3(80f, 0f, 0f), formationIndex: 0);
        battle.CommitAll();
        battle.TickAll(2);
        b.Hull(hullB).IsVacant = true;

        a.ElapseCaptureTimer(hullB, hullA);

        foreach (var peer in new[] { a, b, o })
        {
            Assert.Equal("A", peer.Ship(hullB).CurrentAuthority);
            Assert.Equal("A", peer.Ship(hullB).CaptorControllerId);
            Assert.Equal(BattleSideEnum.Attacker, peer.Hull(hullB).Side);
            Assert.True(peer.Ship(hullA).IsOrphaned);
            Assert.Equal(1, peer.Ships.DepartedHulls(BattleSideEnum.Defender));
        }
        Assert.True(b.Hull(hullB).IsCopy);
        Assert.False(a.Hull(hullB).IsCopy);
        Assert.Single(b.Network.NetworkSentMessages.GetMessages<NetworkShipCaptured>());
    }

    // The victim's crew still holds the hull, so its client refuses and the captor is told; nothing changes anywhere.
    [Fact]
    public void CaptureOfAHullItsCrewStillHolds_IsRefused_AndChangesNothing()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out var o, out var pirates, out var captorHull, out _);
        var prize = pirates[0];

        c.ElapseCaptureTimer(prize, captorHull);

        Assert.Equal(ShipCaptureRefusal.NotVacant,
            Assert.Single(h.Network.NetworkSentMessages.GetMessages<NetworkShipCaptureRejected>()).Reason);
        Assert.Null(PendingAge(c, prize));
        foreach (var peer in new[] { h, c, o })
        {
            Assert.Equal("H", peer.Ship(prize).CurrentAuthority);
            Assert.Null(peer.Ship(prize).CaptorControllerId);
            Assert.False(peer.Ship(captorHull).IsOrphaned);
        }
    }

    // R46: when vanilla's swap can't run on the authority's hulls, it refuses before changing anything, so no client is left
    // with half a capture.
    [Fact]
    public void CaptureWhoseSwapCannotRunAtTheAuthority_IsRefused_AndChangesNothing()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out var o, out var pirates, out var captorHull, out _);
        var prize = pirates[0];
        h.Hull(prize).IsVacant = true;
        h.Engine.CaptureBlocker = "captor_hull_has_no_formation";

        c.ElapseCaptureTimer(prize, captorHull);

        Assert.Equal(ShipCaptureRefusal.SwapUnavailable,
            Assert.Single(h.Network.NetworkSentMessages.GetMessages<NetworkShipCaptureRejected>()).Reason);
        Assert.Empty(h.Network.NetworkSentMessages.GetMessages<NetworkShipCaptured>());
        foreach (var peer in new[] { h, c, o })
        {
            Assert.Equal("H", peer.Ship(prize).CurrentAuthority);
            Assert.Null(peer.Ship(prize).CaptorControllerId);
            Assert.False(peer.Ship(captorHull).IsOrphaned);
            Assert.Empty(peer.Engine.Captures);
        }
    }

    // R46 review: a receiver whose swap can't run yet holds the capture unchanged and applies it whole once the swap can run,
    // so it ends where its peers are instead of diverging for good.
    [Fact]
    public void ReceiverWhoseSwapCannotRunYet_HoldsTheCapture_AndAppliesItOnceTheStateClears()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out var o, out var pirates, out var captorHull, out _);
        var prize = pirates[0];
        h.Hull(prize).IsVacant = true;
        o.Engine.CaptureBlocker = "captor_hull_has_no_formation";

        c.ElapseCaptureTimer(prize, captorHull);
        o.Tick();

        foreach (var peer in new[] { h, c })
            Assert.Equal("C", peer.Ship(prize).CaptorControllerId);
        Assert.Equal("H", o.Ship(prize).CurrentAuthority);
        Assert.Null(o.Ship(prize).CaptorControllerId);
        Assert.Empty(o.Engine.Captures);
        Assert.Equal(1L, (long)CaptureStats(o)["applied"]!["swapsBlocked"]!);

        o.Engine.CaptureBlocker = null;
        o.Tick();

        Assert.Equal("C", o.Ship(prize).CurrentAuthority);
        Assert.Equal("C", o.Ship(prize).CaptorControllerId);
        Assert.Equal(1, o.Ship(prize).CaptureSequence);
        Assert.True(o.Ship(captorHull).IsOrphaned);
        Assert.Single(o.Engine.Captures);
        Assert.True(o.Hull(prize).IsCopy);
    }

    // R46 review: every other client gave the prize to its captor, so a captor whose swap never runs still takes the hull over
    // without vanilla's swap once the wait is out, rather than leave it simulated by nobody.
    [Fact]
    public void CaptorWhoseSwapNeverRuns_TakesTheHullOverWithoutTheSwap_AfterTheWait()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out var o, out var pirates, out var captorHull, out _);
        var prize = pirates[0];
        h.Hull(prize).IsVacant = true;
        c.Engine.CaptureBlocker = "captor_formation_holds_another_hull";

        c.ElapseCaptureTimer(prize, captorHull);
        c.Tick();
        bool captorHeldIt = c.Ship(prize).CurrentAuthority == "H" && c.Hull(prize).IsCopy;
        c.Tick(global::Missions.Battles.ShipCaptureApplier.SwapWaitSeconds + 1f);

        Assert.True(captorHeldIt);
        foreach (var peer in new[] { h, c, o })
        {
            Assert.Equal("C", peer.Ship(prize).CurrentAuthority);
            Assert.Equal("C", peer.Ship(prize).CaptorControllerId);
            Assert.True(peer.Ship(captorHull).IsOrphaned);
        }

        Assert.False(c.Hull(prize).IsCopy);
        Assert.Contains(c.Ship(prize).Hull, c.Engine.Adoptions);
        Assert.Empty(c.Engine.Captures);
        Assert.Equal(1L, (long)CaptureStats(c)["applied"]!["capturesWithoutSwap"]!);
    }

    // (d) C and O race for one hull: the authority confirms the first request, whose broadcast ends O's request too, and O's
    // late request reaches a client that no longer simulates the hull.
    [Fact]
    public void TwoCaptorsRaceForOneHull_OneCaptureAppliesEverywhere_AndTheOtherIsRefused()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out var o, out var pirates, out var captorHull, out _);
        var prize = pirates[0];
        var observerHull = o.Registry.Ships.Single(ship => ship.CurrentAuthority == "O").ShipId;
        h.Hull(prize).IsVacant = true;

        // Both requests are in flight before either captor hears of the other.
        c.Network.RouteMessages = false;
        o.Network.RouteMessages = false;
        c.ElapseCaptureTimer(prize, captorHull);
        o.ElapseCaptureTimer(prize, observerHull);
        c.Network.RouteMessages = true;
        o.Network.RouteMessages = true;
        var requestOfC = c.Network.NetworkSentMessages.GetMessages<NetworkShipCaptureRequest>().Single();
        var requestOfO = o.Network.NetworkSentMessages.GetMessages<NetworkShipCaptureRequest>().Single();
        h.Call(() => h.Instance.SimulateMessage(c.Network.NetPeer, requestOfC));
        h.Call(() => h.Instance.SimulateMessage(o.Network.NetPeer, requestOfO));
        battle.SettleCaptures();

        Assert.Single(h.Network.NetworkSentMessages.GetMessages<NetworkShipCaptured>());
        var refusal = Assert.Single(h.Network.NetworkSentMessages.GetMessages<NetworkShipCaptureRejected>());
        Assert.Equal("O", refusal.CaptorControllerId);
        Assert.Equal(ShipCaptureRefusal.NotAuthority, refusal.Reason);
        Assert.Null(PendingAge(o, prize));
        foreach (var peer in new[] { h, c, o })
        {
            Assert.Equal("C", peer.Ship(prize).CaptorControllerId);
            Assert.False(peer.Ship(observerHull).IsOrphaned);
        }
    }

    // (e) The old host's confirm before the migration: the prize stays with its captor, and only the other AI hull moves.
    [Fact]
    public void CaptureBeforeAHostMigration_ThePrizeStaysWithItsCaptor_AndOnlyTheOtherAiHullMoves()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out var o, out var pirates, out var captorHull, out _);
        h.Hull(pirates[0]).IsVacant = true;
        c.ElapseCaptureTimer(pirates[0], captorHull);

        battle.MigrateHost(h, "O");
        battle.TickAll(4);

        foreach (var peer in new[] { c, o })
        {
            Assert.Equal("C", peer.Ship(pirates[0]).CurrentAuthority);
            Assert.Equal("O", peer.Ship(pirates[1]).CurrentAuthority);
        }
        Assert.True(o.Hull(pirates[0]).IsCopy);
        Assert.False(c.Hull(pirates[0]).IsCopy);
        Assert.False(o.Hull(pirates[1]).IsCopy);
    }

    // R47 bug B: a host migration right after a capture, with the captor as the successor (battle 3), leaves the captor's own
    // crew alone: the departing host's hull and the inherited AI hulls swap without parking, moving or uncrewing the troops
    // that crew the prize from the orphan's deck. Vanilla's own off-ship check is what drops them once the hulls part.
    [Fact]
    public void CaptureThenTheCaptorBecomesHost_TheCaptorsCrewOnTheOrphanStaysThePrizesCrew()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out var o, out var pirates, out var captorHull, out _);
        h.Hull(pirates[0]).IsVacant = true;
        c.ElapseCaptureTimer(pirates[0], captorHull);
        var crew = c.RegisterAgent(Guid.NewGuid(), "C");
        c.Call(() => c.Engine.PlaceAgent(crew, c.Ship(captorHull).Hull, c.Ship(pirates[0]).Hull));
        int movedBefore = c.Engine.MovedAgents;

        battle.MigrateHost(h, "C");
        battle.TickAll(4);

        Assert.Equal("C", c.Ship(pirates[1]).CurrentAuthority);
        Assert.False(c.Hull(pirates[1]).IsCopy);
        Assert.True(crew.IsActive());
        Assert.Same(c.Ship(pirates[0]).Hull, c.Engine.CrewHullOf(crew));
        Assert.Same(c.Hull(captorHull), c.Engine.DeckOf(crew));
        Assert.Equal(movedBefore, c.Engine.MovedAgents);
        Assert.DoesNotContain(crew, c.Engine.AgentsEverParked);
    }

    // (e) The old host's confirm reaches the successor mid-swap and the captor after the old host left: both apply it, so
    // every client ends where the clients that heard it first did.
    [Fact]
    public void ConfirmFromADepartedHost_ArrivingMidSwapOrAfterItLeft_GivesTheSameStateEverywhere()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out var o, out var pirates, out var captorHull, out _);
        var prize = pirates[0];
        h.Hull(prize).IsVacant = true;
        h.Network.RouteMessages = false;
        c.ElapseCaptureTimer(prize, captorHull);
        var confirm = h.Network.NetworkSentMessages.GetMessages<NetworkShipCaptured>().Single();

        battle.MigrateHost(h, "O");
        o.Call(() => o.Instance.SimulateMessage(h.Network.NetPeer, confirm));
        o.TickCaptures();
        bool appliedMidSwap = o.Ship(prize).CaptorControllerId != null;
        battle.TickAll(4);
        c.Call(() => c.Instance.SimulateMessage(h.Network.NetPeer, confirm));
        c.TickCaptures();

        Assert.False(appliedMidSwap);
        foreach (var peer in new[] { c, o })
        {
            Assert.Equal("C", peer.Ship(prize).CurrentAuthority);
            Assert.Equal("C", peer.Ship(prize).CaptorControllerId);
            Assert.True(peer.Ship(captorHull).IsOrphaned);
        }
        Assert.True(o.Hull(prize).IsCopy);
        Assert.False(c.Hull(prize).IsCopy);
        Assert.Contains(o.Ship(prize).Hull, o.Engine.Releases);
        Assert.Null(PendingAge(c, prize));
    }

    // (e) A request the old host never answered expires on the captor and tells the player; the hull stays an AI hull.
    [Fact]
    public void RequestTheOldHostNeverAnswered_Expires_AndTheHullStaysWithTheSuccessor()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out var o, out var pirates, out var captorHull, out _);
        c.Network.RouteMessages = false;
        c.ElapseCaptureTimer(pirates[0], captorHull);
        c.Network.RouteMessages = true;

        battle.MigrateHost(h, "O");
        c.Ticks(130);

        Assert.Null(PendingAge(c, pirates[0]));
        Assert.Equal(1L, (long)CaptureStats(c)["captureExpired"]!);
        Assert.Equal("O", c.Ship(pirates[0]).CurrentAuthority);
        Assert.Null(c.Ship(pirates[0]).CaptorControllerId);
    }

    // (f) The captor disconnects: the prize and its orphan leave every client in one batch, and the captor's crew standing on
    // the orphan is parked as aboard and withdrawn as the prize's crew. R47 bug A: the orphan holds no ship assignment, so
    // the engine removes it through a lent one rather than leave a teamless ghost hull in the scene.
    [Fact]
    public void CaptorDisconnects_ThePrizeAndOrphanLeaveEveryClient_WithItsCrewOnTheOrphanWithdrawn()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out var o, out var pirates, out var captorHull, out _);
        var prize = pirates[0];
        h.Hull(prize).IsVacant = true;
        c.ElapseCaptureTimer(prize, captorHull);
        var crewOnOrphan = StandCrewOnOrphan(new[] { h, o }, prize, captorHull);
        var orphans = new[] { h, o }.ToDictionary(peer => peer, peer => peer.Hull(captorHull));

        battle.Disconnect(c);
        battle.TickAll(3);

        foreach (var peer in new[] { h, o })
        {
            Assert.False(peer.Has(prize));
            Assert.False(peer.Has(captorHull));
            Assert.True(orphans[peer].IsRemoved, $"{peer.Id} kept the captor's orphan in its scene");
            Assert.True(orphans[peer].RemovedThroughLentSlot);
            Assert.Equal(0, peer.Engine.LentSlotsHeld);
            Assert.False(crewOnOrphan[peer].IsActive());
            Assert.Empty(peer.Engine.ParkedAgents);
            Assert.True(peer.Has(pirates[1]));
        }
    }

    // (f, R47 bug A) The captor leaves or retreats: its mission end announces only the prize, since vanilla's removal never
    // reaches the orphan (it holds no ship assignment); every other client still removes the orphan when the captor leaves,
    // after its deck is cleared, so no teamless ghost hull stays in their scenes.
    [Theory]
    [InlineData(ShipRemovalReason.Leave)]
    [InlineData(ShipRemovalReason.Retreat)]
    public void CaptorLeaves_ThePrizeIsAnnounced_AndTheOrphanVanillaKeepsIsRemovedOnEveryOtherClient(ShipRemovalReason reason)
    {
        using var battle = StartCaptureBattle(out var h, out var c, out var o, out var pirates, out var captorHull, out _);
        var prize = pirates[0];
        h.Hull(prize).IsVacant = true;
        c.ElapseCaptureTimer(prize, captorHull);
        var crewOnOrphan = StandCrewOnOrphan(new[] { h, o }, prize, captorHull);
        var orphans = new[] { h, o }.ToDictionary(peer => peer, peer => peer.Hull(captorHull));

        // NavalShipsLogic.OnEndMission fires BeforeShipRemovedEvent only for hulls its RemoveShip finds, so not the orphan.
        c.Call(() =>
        {
            foreach (var ship in c.Registry.Ships.Where(ship => ship.CurrentAuthority == "C").ToList())
            {
                c.Engine.VanillaOwnerRemoveShipForTest(ship.Hull);
                if (c.Engine.Of(ship.Hull).IsRemoved) c.Ships.AnnounceRemoval(ship.Hull, reason, missionEnding: true);
            }
        });
        Assert.Single(c.Network.NetworkSentMessages.GetMessages<NetworkShipRemoved>());
        battle.TickAll(2);
        Assert.All(new[] { h, o }, peer => Assert.False(orphans[peer].IsRemoved));

        battle.Leave(c);
        battle.TickAll(4);

        foreach (var peer in new[] { h, o })
        {
            Assert.False(peer.Has(prize));
            Assert.False(peer.Has(captorHull));
            Assert.True(orphans[peer].IsRemoved, $"{peer.Id} kept the captor's orphan in its scene");
            Assert.True(orphans[peer].RemovedThroughLentSlot);
            Assert.Equal(0, peer.Engine.LentSlotsHeld);
            Assert.False(crewOnOrphan[peer].IsActive());
            Assert.Empty(peer.Engine.ParkedAgents);
            Assert.True(peer.Has(pirates[1]));
        }
    }

    // (g) A player joining after the capture spawns the prize as the captor's hull on its side and the orphan without a formation.
    [Fact]
    public void LateJoinerAfterACapture_SpawnsThePrizeAsTheCaptorsHull_AndTheOrphanWithoutAFormation()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out _, out var pirates, out var captorHull, out _,
            lateJoiner: "O");
        var prize = pirates[0];
        h.Hull(prize).IsVacant = true;
        c.ElapseCaptureTimer(prize, captorHull);

        var joiner = battle.Join("O");
        joiner.Commit();
        battle.AnnounceEntry(joiner);
        battle.TickAll(2);

        var ship = joiner.Ship(prize);
        Assert.Equal("C", ship.CurrentAuthority);
        Assert.Equal("C", ship.CaptorControllerId);
        Assert.False(ship.FollowsBattleHost);
        Assert.Equal(BattleSideEnum.Attacker, joiner.Hull(prize).Side);
        Assert.True(joiner.Ship(captorHull).IsOrphaned);
        Assert.Null(joiner.Ship(captorHull).Formation);
        Assert.True(joiner.Hull(captorHull).IsOrphaned);
        Assert.Equal("H", joiner.Ship(pirates[1]).CurrentAuthority);
    }

    // (g, C3) A joiner still deploying holds the old authority's record of the prize when the capture arrives: it applies the
    // capture once the copy spawned, however long the deployment took.
    [Fact]
    public void LateJoinerHoldingThePreCaptureRecord_TakesTheCaptureOnceTheCopySpawned()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out _, out var pirates, out var captorHull, out _,
            lateJoiner: "O");
        var prize = pirates[0];
        var joiner = battle.Join("O");
        joiner.Engine.IsDeploymentMode = true;
        joiner.Commit();
        battle.AnnounceEntry(joiner);
        battle.TickAll(2);
        Assert.False(joiner.Has(prize));

        h.Hull(prize).IsVacant = true;
        c.ElapseCaptureTimer(prize, captorHull);
        joiner.Ticks(700);
        joiner.Engine.IsDeploymentMode = false;
        battle.TickAll(2);

        Assert.Equal("C", joiner.Ship(prize).CurrentAuthority);
        Assert.Equal("C", joiner.Ship(prize).CaptorControllerId);
        Assert.Equal(BattleSideEnum.Attacker, joiner.Hull(prize).Side);
        Assert.True(joiner.Ship(captorHull).IsOrphaned);
        Assert.Equal(1, joiner.Ship(prize).CaptureSequence);
        // The captor's records sent again after the capture replace the held ones, so the copy spawns captured and the
        // confirm held for it is a repeat; either way the capture lands once and nothing is left waiting.
        var applied = CaptureStats(joiner)["applied"]!;
        Assert.Empty(applied["heldConfirms"]!);
        Assert.Equal(0L, (long)applied["captureConflicts"]!);
    }

    // (h) Q5: once the last AI hull is captured the host counts no hull left on the AI side and one gone from it, which is
    // vanilla's retreat (CoopNavalBattleEndLogic.KeepsRetreat), while the prize counts for the players' side.
    [Fact]
    public void LastAiHullCaptured_TheHostsTallyHasNoAiHullLeftAndOneGoneFromThatSide()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out _, out var pirates, out var captorHull, out var hostHull);
        h.Hull(pirates[0]).IsVacant = true;
        h.Hull(pirates[1]).IsVacant = true;

        c.ElapseCaptureTimer(pirates[0], captorHull);
        h.ElapseCaptureTimer(pirates[1], hostHull);

        var enemy = EndTally(h, BattleSideEnum.Defender, liveHumans: 0);
        var players = EndTally(h, BattleSideEnum.Attacker, liveHumans: 3);
        Assert.Equal(0, enemy.Hulls);
        Assert.Equal(2, h.Ships.DepartedHulls(BattleSideEnum.Defender));
        Assert.Equal(3, players.Hulls);
    }

    // C3: a joiner whose link to the host missed the confirm takes the capture from the captor's records, which the captor
    // sends again once it applied a capture.
    [Fact]
    public void LateJoinerThatMissedTheConfirm_TakesTheCaptureFromTheCaptorsRecords()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out _, out var pirates, out var captorHull, out _,
            lateJoiner: "O");
        var prize = pirates[0];
        var joiner = battle.Join("O");
        joiner.Commit();
        battle.AnnounceEntry(joiner);
        battle.TickAll(2);
        Assert.Equal("H", joiner.Ship(prize).CurrentAuthority);

        h.Hull(prize).IsVacant = true;
        h.Network.RouteMessages = false;
        c.ElapseCaptureTimer(prize, captorHull);
        h.Network.RouteMessages = true;
        var confirm = h.Network.NetworkSentMessages.GetMessages<NetworkShipCaptured>().Single();
        c.Call(() => c.Instance.SimulateMessage(h.Network.NetPeer, confirm));
        battle.TickAll(2);

        Assert.Equal("C", joiner.Ship(prize).CurrentAuthority);
        Assert.Equal("C", joiner.Ship(prize).CaptorControllerId);
        Assert.Equal(1, joiner.Ship(prize).CaptureSequence);
        Assert.True(joiner.Ship(captorHull).IsOrphaned);
        Assert.Equal(BattleSideEnum.Attacker, joiner.Hull(prize).Side);
        Assert.Equal(1L, (long)CaptureStats(joiner)["applied"]!["recordsApplied"]!);
        Assert.Equal(1L, (long)CaptureStats(h)["applied"]!["capturesApplied"]!);
    }

    // C2: vanilla's swap points the old owner's crew record of the prize at the captor's orphan, which has no team; when the
    // captor leaves, the host's swimmers tracked there stay in the battle instead of withdrawing with the orphan.
    [Fact]
    public void CaptorLeaves_TheOldOwnersSwimmersTrackedOnTheOrphanStayInTheBattle()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out _, out var pirates, out var captorHull, out _);
        var prize = pirates[0];
        var swimmer = h.RegisterAgent(Guid.NewGuid(), "H", h.Mission.DefenderTeam);
        h.Engine.AssignCrew(swimmer, h.Ship(prize).Hull);
        h.Hull(prize).IsVacant = true;
        c.ElapseCaptureTimer(prize, captorHull);

        battle.Disconnect(c);
        battle.TickAll(3);

        Assert.False(h.Has(prize));
        Assert.False(h.Has(captorHull));
        Assert.True(swimmer.IsActive(), "the old owner's swimmer withdrew with the captor's orphan");
        Assert.DoesNotContain(swimmer, h.Engine.Withdrawn);
    }

    // C4: the PvP victim confirms and leaves at once; an observer whose departure of the victim came before the confirm took
    // the prize out with it, and spawns it again from the captor's records as the captor's hull.
    [Fact]
    public void VictimLeavesRightAfterConfirming_AnObserverThatRemovedThePrizeSpawnsItAgainAsTheCaptors()
    {
        using var battle = StartNavalBattle(new[]
        {
            ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Defender), ("O", BattleSideEnum.Attacker),
        });
        var (a, b, o) = (battle["A"], battle["B"], battle["O"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f), formationIndex: 0);
        var hullB = battle.FieldPlayerHull(b, new Vec3(0f, 40f, 0f), formationIndex: 0);
        battle.FieldPlayerHull(o, new Vec3(80f, 0f, 0f), formationIndex: 0);
        battle.CommitAll();
        battle.TickAll(2);
        b.Hull(hullB).IsVacant = true;

        b.Network.RouteMessages = false;
        a.ElapseCaptureTimer(hullB, hullA);
        var confirm = b.Network.NetworkSentMessages.GetMessages<NetworkShipCaptured>().Single();
        a.Network.RouteMessages = false;
        a.Call(() => a.Instance.SimulateMessage(b.Network.NetPeer, confirm));
        a.TickCaptures();
        a.Network.RouteMessages = true;
        var records = a.Network.NetworkSentMessages.GetMessages<NetworkSpawnBattleShips>().Last();
        battle.Disconnect(b);
        battle.TickAll(3);
        Assert.False(o.Has(hullB));

        o.Call(() => o.Instance.SimulateMessage(a.Network.NetPeer, records));
        battle.TickAll(2);
        o.Call(() => o.Instance.SimulateMessage(b.Network.NetPeer, confirm));
        o.TickCaptures();

        Assert.Equal("A", o.Ship(hullB).CurrentAuthority);
        Assert.Equal("A", o.Ship(hullB).CaptorControllerId);
        Assert.Equal(1, o.Ship(hullB).CaptureSequence);
        Assert.Equal(BattleSideEnum.Attacker, o.Hull(hullB).Side);
        Assert.True(o.Ship(hullA).IsOrphaned);
        Assert.Equal(1, o.Ships.DepartedHulls(BattleSideEnum.Defender));
        Assert.True(a.Has(hullB));
    }

    // A joiner counts every capture made before it joined as a hull gone from its side, so once it hosts, the retreat rule
    // ends the battle when the last AI hull was captured.
    [Fact]
    public void LateJoinerThatBecomesHost_CountsTheCapturesMadeBeforeItJoined_ForTheRetreatRule()
    {
        using var battle = StartCaptureBattle(out var h, out var c, out _, out var pirates, out var captorHull, out _,
            lateJoiner: "O");
        h.Hull(pirates[0]).IsVacant = true;
        h.Hull(pirates[1]).IsVacant = true;
        c.ElapseCaptureTimer(pirates[0], captorHull);
        c.ElapseCaptureTimer(pirates[1], pirates[0]);

        var joiner = battle.Join("O");
        joiner.Commit();
        battle.AnnounceEntry(joiner);
        battle.TickAll(2);
        battle.MigrateHost(h, "O");
        battle.TickAll(3);

        Assert.Equal(0, EndTally(joiner, BattleSideEnum.Defender, liveHumans: 0).Hulls);
        Assert.Equal(2, joiner.Ships.DepartedHulls(BattleSideEnum.Defender));
        Assert.Equal("C", joiner.Ship(pirates[1]).CaptorControllerId);
        Assert.True(joiner.Ship(pirates[0]).IsOrphaned);
    }

    // R48 L6, coop layer only: a non-host captor takes the last afloat AI hull after the other AI hull sank. The prize's authority
    // moves off the battle host, both clients apply the capture once from their ticks and hold nothing, the host's tally reads the
    // AI side out of action with swimmers counted alive, and the result concludes the battle once. The fake engine runs no
    // formation AI, so the swimmers' move into the prize's formation and the behaviour reset are not exercised here.
    [Fact]
    public void NonHostCaptorTakesTheLastAfloatAiHull_AfterTheOtherSank_AppliesOnceAndConcludesOnce()
    {
        using var battle = StartServerBattle(atSea: true, out var concluded, "H", "C");
        var (h, c) = (battle["H"], battle["C"]);
        battle.FieldPlayerHull(h, new Vec3(0f, 0f, 0f), formationIndex: 0);
        var captorHull = battle.FieldPlayerHull(c, new Vec3(40f, 0f, 0f), formationIndex: 0);
        var pirates = Enumerable.Range(0, 2)
            .Select(i => battle.FieldAiHull(h, BattleSideEnum.Defender, new Vec3(30f * i, 300f, 0f), formationIndex: i,
                campaignShipId: $"Ship_pirate_{i}"))
            .ToArray();
        battle.CommitAll();
        battle.TickAll(2);
        c.Hit(pirates[0], 150f);
        battle.TickAll(2);
        h.Hull(pirates[1]).IsVacant = true;

        c.ElapseCaptureTimer(pirates[1], captorHull);
        battle.TickAll(30);

        foreach (var peer in new[] { h, c })
        {
            Assert.Equal("C", peer.Ship(pirates[1]).CurrentAuthority);
            Assert.True(peer.Ship(captorHull).IsOrphaned);
            var applied = CaptureStats(peer)["applied"]!;
            Assert.Equal(1L, (long)applied["capturesApplied"]!);
            Assert.Empty(applied["heldConfirms"]!);
            Assert.Empty(applied["heldRecords"]!);
            Assert.Equal(1, peer.Ships.DepartedHulls(BattleSideEnum.Defender));
            Assert.Equal(BattleShipCondition.Sunk, peer.Hull(pirates[0]).SinkingState);
        }
        Assert.True(h.Hull(pirates[1]).IsCopy);
        Assert.False(c.Hull(pirates[1]).IsCopy);

        var pirateSide = EndTally(h, BattleSideEnum.Defender, liveHumans: 9);
        Assert.Equal(0, pirateSide.HullsAfloat);
        Assert.True(pirateSide.IsOutOfAction);
        Assert.False(pirateSide.IsDepleted);
        Assert.Equal(0, concluded());

        ResolveOnHost(h, BattleState.AttackerVictory);
        c.Call(() => c.Controller.ResultCommitter.ReportAcceptedResult(), VictoryConclusionDisabledMethods());

        Assert.Equal(1, concluded());
        AssertMapEventRemoved(battle.MapEventId);
    }

    // H, C and O each field a hull on formation 0 of the attacking team; H fields two AI hulls, each standing for a pirate ship.
    private NavalBattle StartCaptureBattle(out NavalPeer h, out NavalPeer c, out NavalPeer o, out Guid[] pirates,
        out Guid captorHull, out Guid hostHull, string? lateJoiner = null)
    {
        var battle = StartNavalBattle(new[]
        {
            ("H", BattleSideEnum.Attacker), ("C", BattleSideEnum.Attacker), ("O", BattleSideEnum.Attacker),
        }, hostEpoch: 1, lateJoiner == null ? Array.Empty<string>() : new[] { lateJoiner });
        h = battle["H"];
        c = battle["C"];
        o = lateJoiner == null ? battle["O"] : null!;
        hostHull = battle.FieldPlayerHull(h, new Vec3(0f, 0f, 0f), formationIndex: 0);
        captorHull = battle.FieldPlayerHull(c, new Vec3(40f, 0f, 0f), formationIndex: 0);
        if (o != null) battle.FieldPlayerHull(o, new Vec3(80f, 0f, 0f), formationIndex: 0);
        var host = h;
        pirates = Enumerable.Range(0, 2)
            .Select(i => battle.FieldAiHull(host, BattleSideEnum.Defender, new Vec3(30f * i, 300f, 0f), formationIndex: i,
                campaignShipId: $"Ship_pirate_{i}"))
            .ToArray();
        battle.CommitAll();
        battle.TickAll(2);
        return battle;
    }

    // A puppet of the captor's crew, crewing the prize, standing on the orphan's deck on each observer.
    private static Dictionary<NavalPeer, Agent> StandCrewOnOrphan(IEnumerable<NavalPeer> observers, Guid prize, Guid orphan)
    {
        var crewId = Guid.NewGuid();
        return observers.ToDictionary(peer => peer, peer =>
        {
            var puppet = peer.RegisterAgent(crewId, "C");
            peer.Engine.PlaceAgent(puppet, peer.Ship(orphan).Hull, peer.Ship(prize).Hull);
            return puppet;
        });
    }

    private static JObject CaptureStats(NavalPeer peer)
    {
        JObject stats = null!;
        peer.Call(() => stats = JObject.FromObject(peer.Captures.Inspect()));
        return stats;
    }

    private static float? PendingAge(NavalPeer peer, Guid shipId)
    {
        float? age = null;
        peer.Call(() => age = peer.Captures.PendingRequestAge(shipId));
        return age;
    }
}
