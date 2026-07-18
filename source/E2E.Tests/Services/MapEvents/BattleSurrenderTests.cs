using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// Section 7 (Surrender) requirements driven through the REAL surrender request path rather than an
/// injected <see cref="PlayerSurrendered"/> event:
/// <list type="bullet">
/// <item>BR-060 — a participating player's native surrender action forwards exactly one surrender request.</item>
/// <item>BR-061 — the final result records the surrendered heroes AND troops as prisoners: the leader via
/// its own capture, the regular troops via the roster transfer, and companion heroes riding in the party
/// via individual <c>TakePrisonerAction</c> captures (dead companions excepted).</item>
/// <item>BR-063 — one side surrendering before the mission begins resolves the battle without the opposing
/// side ever entering/loading a mission.</item>
/// </list>
/// Built on the two-opposing-player setup used by <c>CoopBattleFinalizeTests</c>; each client controls its
/// own <see cref="MobileParty.MainParty"/> so the per-client encounter/surrender paths run for real.
/// </summary>
public class BattleSurrenderTests : MapEventTestBase
{
    public BattleSurrenderTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// BR-060: a participating player is permitted to surrender. Driving the ACTUAL patched
    /// <c>PlayerEncounter.PlayerSurrenderInternal</c> (the menu action) on the participating player's client —
    /// not a hand-published <see cref="PlayerSurrendered"/> — forwards exactly one
    /// <see cref="NetworkPlayerSurrendered"/> naming that player's own party, and no one else surrenders on
    /// its behalf.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-060")]
    public void RealSurrenderAction_ByParticipatingPlayer_ForwardsExactlyOneSurrenderRequest()
    {
        var setup = SetupTwoOpposingPlayersInBattle();
        var recipientClient = Clients.Last();

        // The recipient sits at its battle encounter with the shared map event attached — the state the
        // native surrender menu action reads to build the surrender request.
        SetMockPlayerEncounter(Clients.First());
        SetMockPlayerEncounter(recipientClient, mapEventId: setup.ctx.MapEventId);

        recipientClient.Call(() =>
        {
            var encounter = PlayerEncounter.Current;
            Assert.NotNull(encounter);
            InvokePatchedSurrender(encounter);
        }, BattleMenuSurrenderDisabledMethods());

        // Exactly one surrender request was forwarded, carrying the participating (surrendering) player's party.
        var request = Assert.Single(recipientClient.NetworkSentMessages.GetMessages<NetworkPlayerSurrendered>());
        Assert.Equal(setup.recipientPartyId, request.PlayerParty);
        Assert.Equal(setup.ctx.MapEventId, request.MapEventId);

        // No other client surrendered on the participating player's behalf.
        Assert.Empty(Clients.First().NetworkSentMessages.GetMessages<NetworkPlayerSurrendered>());
    }

    /// <summary>
    /// BR-061: the final battle result records the surrendered hero as a prisoner of the captor. Driven from
    /// the real surrender action, the surrendered hero is captive of the captor party on the server and every
    /// client, and its party has left the battle.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-061")]
    public void RecipientSurrender_RecordsSurrenderedHeroAsPrisoner_OnAllInstances()
    {
        var setup = SetupTwoOpposingPlayersInBattle();
        var recipientClient = Clients.Last();

        SetMockPlayerEncounter(Clients.First());
        SetMockPlayerEncounter(recipientClient, mapEventId: setup.ctx.MapEventId);

        recipientClient.Call(() =>
        {
            var encounter = PlayerEncounter.Current;
            Assert.NotNull(encounter);
            InvokePatchedSurrender(encounter);
        }, BattleMenuSurrenderDisabledMethods());

        // The surrendered hero is recorded as a prisoner of the captor everywhere.
        AssertCaptivity(Server, setup.recipientHeroId, setup.initiatorPartyId);
        foreach (var client in Clients)
            AssertCaptivity(client, setup.recipientHeroId, setup.initiatorPartyId);

        // ...and the surrender resolved the battle: the captor (initiator) is no longer in it.
        AssertMainPartyLeftBattle(Clients.First());
    }

    /// <summary>
    /// BR-061: the final result records surrendered HEROES AND TROOPS as prisoners. The server transfers the
    /// surrendered party's remaining regular troops into the captor's prison roster (wounded stay wounded)
    /// before parking the party, and the roster additions replicate, so the captor's prison roster holds the
    /// surrendered troops on every instance.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-061")]
    public void RecipientSurrender_RecordsSurrenderedTroopsAsPrisonersOfCaptor()
    {
        var setup = SetupTwoOpposingPlayersInBattle();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        // The surrendering (recipient) party fields three ordinary troops alongside its hero.
        SeedPartyTroopOnAll(setup.recipientPartyId, troopId, 3);

        PublishRecipientSurrender(setup);

        // BR-061: the surrendered troops are recorded as prisoners of the captor, everywhere.
        AssertCaptorPrisonerCount(Server, setup.initiatorPartyId, troopId, 3);
        foreach (var client in Clients)
            AssertCaptorPrisonerCount(client, setup.initiatorPartyId, troopId, 3);
    }

    /// <summary>
    /// BR-061: the final result records surrendered HEROES as prisoners — including companion heroes riding
    /// in the surrendered party, not just its leader. A companion (a registered hero whose character sits in
    /// the member roster alongside regular troops) must end up a proper hero prisoner of the captor
    /// (captivity state synced everywhere), and the captor's prison roster must gain the companion on top of
    /// the leader and the transferred troops. Guards the BR-061 residual where companions were silently
    /// discarded by the roster emptying (not captured, not freed, not killed).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-061")]
    public void RecipientSurrender_RecordsCompanionHeroAsPrisonerOfCaptor_OnAllInstances()
    {
        var setup = SetupTwoOpposingPlayersInBattle();
        var companionHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        // The surrendering (recipient) party fields a companion hero AND three ordinary troops
        // alongside its leader hero.
        SeedCompanionInParty(companionHeroId, setup.recipientPartyId);
        SeedPartyTroopOnAll(setup.recipientPartyId, troopId, 3);

        // Baselines — harness rosters are nondeterministic, so all count asserts are baseline-relative.
        // Every live hero riding in the party is captured individually (the registered leader natively, the
        // companion and the harness party's own bootstrap lord via the companion capture), so hero
        // expectations are counted from the roster, never hard-coded.
        var surrenderedTroops = GetPartyNonHeroManCount(Server, setup.recipientPartyId);
        var surrenderedHeroes = GetPartyLiveHeroCount(Server, setup.recipientPartyId);
        Assert.True(surrenderedHeroes >= 2, "setup must field at least the leader and the companion");
        var serverPrisonBefore = GetPartyPrisonerCount(Server, setup.initiatorPartyId);
        var clientPrisonBefore = Clients.ToDictionary(c => c, c => GetPartyPrisonerCount(c, setup.initiatorPartyId));

        PublishRecipientSurrender(setup);
        TestEnvironment.FlushCoalescer();

        // The companion is a proper hero prisoner of the captor everywhere (BR-061 "heroes" clause)...
        AssertCaptivity(Server, companionHeroId, setup.initiatorPartyId);
        foreach (var client in Clients)
            AssertCaptivity(client, companionHeroId, setup.initiatorPartyId);

        // ...and the leader's capture still works exactly as before, alongside the companion's.
        AssertCaptivity(Server, setup.recipientHeroId, setup.initiatorPartyId);
        foreach (var client in Clients)
            AssertCaptivity(client, setup.recipientHeroId, setup.initiatorPartyId);

        // The captor's prison roster gained the surrendered troops plus EVERY live hero that rode in the party.
        AssertPartyPrisonerCount(Server, setup.initiatorPartyId, serverPrisonBefore + surrenderedTroops + surrenderedHeroes);
        foreach (var client in Clients)
            AssertPartyPrisonerCount(client, setup.initiatorPartyId, clientPrisonBefore[client] + surrenderedTroops + surrenderedHeroes);

        // The regular-troop transfer is unchanged: the seeded troops are the captor's prisoners everywhere.
        AssertCaptorPrisonerCount(Server, setup.initiatorPartyId, troopId, 3);
        foreach (var client in Clients)
            AssertCaptorPrisonerCount(client, setup.initiatorPartyId, troopId, 3);
    }

    /// <summary>
    /// BR-061 re-entrancy: capturing a companion from inside the surrender processing re-publishes
    /// <c>PrisonerTaken</c> for the same player party (the companion's <c>PartyBelongedTo</c> IS that party,
    /// so the TakePrisonerAction postfix fires again). The surrender must still be processed exactly once:
    /// exact prison-roster deltas (a nested pass would double the troop transfer), the surrendered party's
    /// roster at exactly zero everywhere (never negative), and the party parked.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-061")]
    public void RecipientSurrenderWithCompanion_ProcessesSurrenderExactlyOnce()
    {
        var setup = SetupTwoOpposingPlayersInBattle();
        var companionHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        SeedCompanionInParty(companionHeroId, setup.recipientPartyId);
        SeedPartyTroopOnAll(setup.recipientPartyId, troopId, 3);

        var surrenderedTroops = GetPartyNonHeroManCount(Server, setup.recipientPartyId);
        var surrenderedHeroes = GetPartyLiveHeroCount(Server, setup.recipientPartyId);
        var serverPrisonBefore = GetPartyPrisonerCount(Server, setup.initiatorPartyId);
        var clientPrisonBefore = Clients.ToDictionary(c => c, c => GetPartyPrisonerCount(c, setup.initiatorPartyId));

        PublishRecipientSurrender(setup);
        TestEnvironment.FlushCoalescer();

        // Exactly one processing pass: the troop transfer ran once — a re-entrant nested pass would show up
        // here as a doubled troop element (6) and a doubled total delta.
        AssertCaptorPrisonerCount(Server, setup.initiatorPartyId, troopId, 3);
        AssertPartyPrisonerCount(Server, setup.initiatorPartyId, serverPrisonBefore + surrenderedTroops + surrenderedHeroes);
        foreach (var client in Clients)
        {
            AssertCaptorPrisonerCount(client, setup.initiatorPartyId, troopId, 3);
            AssertPartyPrisonerCount(client, setup.initiatorPartyId, clientPrisonBefore[client] + surrenderedTroops + surrenderedHeroes);
        }

        // The surrendered party's member roster is exactly zero — no double removal driving it negative,
        // no leftover companion element — on the server and every client.
        AssertPartyManCount(Server, setup.recipientPartyId, 0);
        foreach (var client in Clients)
            AssertPartyManCount(client, setup.recipientPartyId, 0);

        // The party was parked (IsActive is server-local state, so it is asserted on the server only).
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(setup.recipientPartyId, out var party));
            Assert.False(party.IsActive, "surrendered player party should be parked");
        });
    }

    /// <summary>
    /// BR-061 boundary: a DEAD companion hero must never be taken prisoner. With a dead companion's element
    /// still in the surrendered party's roster, the surrender captures only the leader (prison delta =
    /// troops + 1) and the dead companion stays out of captivity everywhere. Pins the aliveness guard of the
    /// companion capture (wounded companions are captured — aliveness is the only bar, matching native).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-061")]
    public void RecipientSurrender_DoesNotCaptureDeadCompanionHero()
    {
        var setup = SetupTwoOpposingPlayersInBattle();
        var companionHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        SeedCompanionInParty(companionHeroId, setup.recipientPartyId);
        SeedPartyTroopOnAll(setup.recipientPartyId, troopId, 3);

        // The companion died before the surrender resolved (its roster element is still present).
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));
            companion.ChangeState(Hero.CharacterStates.Dead);
        }, MapEventDisabledMethods);

        // Counted AFTER the death: the live-hero count excludes the dead companion, so the expected prison
        // delta is exactly "everyone but the dead hero".
        var surrenderedTroops = GetPartyNonHeroManCount(Server, setup.recipientPartyId);
        var surrenderedLiveHeroes = GetPartyLiveHeroCount(Server, setup.recipientPartyId);
        var serverPrisonBefore = GetPartyPrisonerCount(Server, setup.initiatorPartyId);

        PublishRecipientSurrender(setup);
        TestEnvironment.FlushCoalescer();

        // Only the live heroes were captured beyond the troops; the dead companion is not a prisoner anywhere.
        AssertPartyPrisonerCount(Server, setup.initiatorPartyId, serverPrisonBefore + surrenderedTroops + surrenderedLiveHeroes);
        AssertCaptivity(Server, companionHeroId, null);
        foreach (var client in Clients)
            AssertCaptivity(client, companionHeroId, null);
    }

    /// <summary>
    /// BR-061 boundary: a companion killed DURING the battle must never be taken prisoner — even though it
    /// still reports <c>IsAlive == true</c> at capture time. During an active map event native
    /// <c>KillCharacterAction.ApplyInternal</c> defers the kill: it only records a battle <c>DeathMark</c>
    /// (<c>DiedInBattle</c>) and returns while the victim's party still has a <c>MapEvent</c>, so the hero's
    /// state stays Active and <c>IsAlive</c> is still true when the surrender is processed. Native (and the
    /// coop <c>MapEventResultsInterface</c> reimplementation of <c>CaptureDefeatedPartyMembers</c>) gate
    /// capture on the battle DeathMark, not on aliveness; the companion capture must do the same.
    /// <para>
    /// This is the reviewer's exact scenario, distinct from
    /// <see cref="RecipientSurrender_DoesNotCaptureDeadCompanionHero"/> (which sets the state to Dead, so
    /// <c>IsAlive</c> is already false and the old aliveness guard alone catches it). Pre-fix mechanism:
    /// <c>CaptureCompanionHeroes</c> skipped only <c>!IsAlive</c>/<c>IsPrisoner</c>, so this DeathMarked-but-alive
    /// companion PASSES the guard, <c>TakePrisonerAction.Apply</c> runs, and it becomes a prisoner of the
    /// captor — failing <c>AssertCaptivity(companionHeroId, null)</c>. With the DeathMark check added
    /// (<c>HasBattleDeathMark</c>), the companion is skipped and stays free.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-061")]
    public void RecipientSurrender_DoesNotCaptureBattleDeadCompanionHero_WhenIsAliveStillTrue()
    {
        var setup = SetupTwoOpposingPlayersInBattle();
        var companionHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        SeedCompanionInParty(companionHeroId, setup.recipientPartyId);
        SeedPartyTroopOnAll(setup.recipientPartyId, troopId, 3);

        // The companion was killed in THIS battle. Native defers the kill while the map event is active — only a
        // battle DeathMark is recorded, and the hero is still IsAlive == true when the surrender is processed.
        // This is exactly the state the aliveness-only guard failed to treat as dead.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));
            companion.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.DiedInBattle);

            Assert.True(companion.IsAlive, "battle death is deferred mid-map-event: the companion must still be IsAlive here");
            Assert.Equal(KillCharacterAction.KillCharacterActionDetail.DiedInBattle, companion.DeathMark);
        }, MapEventDisabledMethods);

        // Counted AFTER the death mark: capturable heroes exclude the battle-dead companion (aliveness alone
        // would still count it), so the expected prison delta is exactly "everyone capturable but the dead hero".
        var surrenderedTroops = GetPartyNonHeroManCount(Server, setup.recipientPartyId);
        var capturableHeroes = GetPartyCapturableHeroCount(Server, setup.recipientPartyId);
        var serverPrisonBefore = GetPartyPrisonerCount(Server, setup.initiatorPartyId);

        PublishRecipientSurrender(setup);
        TestEnvironment.FlushCoalescer();

        // The battle-dead companion is NOT a prisoner anywhere (the reviewer's exact scenario).
        AssertCaptivity(Server, companionHeroId, null);
        foreach (var client in Clients)
            AssertCaptivity(client, companionHeroId, null);

        // The leader's capture still works — the surrender WAS processed; only the dead companion was skipped.
        AssertCaptivity(Server, setup.recipientHeroId, setup.initiatorPartyId);

        // Only the capturable (live, un-death-marked) heroes were captured beyond the troops.
        AssertPartyPrisonerCount(Server, setup.initiatorPartyId, serverPrisonBefore + surrenderedTroops + capturableHeroes);
    }

    /// <summary>
    /// BR-063: if one side surrenders before the battle mission begins, the battle is resolved without the
    /// opposing side entering or finishing loading the mission. With NO mission open on either client, the
    /// recipient surrenders and the battle resolves everywhere (map event removed, surrendered hero captive)
    /// while no <see cref="NetworkStartAttackMission"/> is ever broadcast — i.e. no one loads a battle mission.
    /// (A <see cref="PlayerEncounter"/> here is the campaign-map encounter menu, not a battle mission.)
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-063")]
    public void SideSurrendersBeforeMissionEntry_ResolvesBattle_WithoutOpposingSideEnteringMission()
    {
        var setup = SetupTwoOpposingPlayersInBattle();

        // Both players sit at the campaign encounter menu; neither has opened a battle mission.
        SetMockPlayerEncounter(Clients.First());
        SetMockPlayerEncounter(Clients.Last());

        PublishRecipientSurrender(setup);

        // The battle was resolved from the surrender alone: the shared map event is gone everywhere and the
        // surrendered hero is captive of the captor everywhere.
        AssertMapEventRemoved(Server, setup.ctx.MapEventId);
        foreach (var client in Clients)
            AssertMapEventRemoved(client, setup.ctx.MapEventId);

        AssertCaptivity(Server, setup.recipientHeroId, setup.initiatorPartyId);
        foreach (var client in Clients)
            AssertCaptivity(client, setup.recipientHeroId, setup.initiatorPartyId);

        // No battle mission was ever started for the opposing side (or anyone): the resolution required no
        // mission entry/load.
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>());
        foreach (var client in Clients)
            Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkStartAttackMission>());
    }

    /// <summary>
    /// Partial surrender: while a healthy ally still fights on the surrenderer's side, the surrender must
    /// capture ONLY the surrendering party — a whole-side <c>DoSurrender</c> would mark the side
    /// surrendered and hand the other side the win, ending the battle for the healthy ally. The event
    /// survives with the ally still on its side, the surrendered party leaves it everywhere, and its hero
    /// becomes the enemy leader's prisoner.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-061")]
    public void RecipientSurrender_WithHealthyAllyOnSide_CapturesOnlyTheSurrenderer()
    {
        var setup = SetupTwoOpposingPlayersInBattle();

        // A healthy ally party fights on the recipient's (defender) side.
        var allyPartyId = JoinNewServerPartyToSide(setup.ctx.MapEventId, BattleSideEnum.Defender);
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        SeedPartyTroopOnAll(allyPartyId, troopId, 3);

        Server.NetworkSentMessages.Clear();

        PublishRecipientSurrender(setup);
        TestEnvironment.FlushCoalescer();

        // The surrenderer's hero is the enemy (attacker) leader's prisoner everywhere...
        AssertCaptivity(Server, setup.recipientHeroId, setup.initiatorPartyId);
        foreach (var client in Clients)
            AssertCaptivity(client, setup.recipientHeroId, setup.initiatorPartyId);

        // ...and ONLY the surrenderer left the battle: the event survives with the ally still on its side.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(setup.ctx.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(allyPartyId, out var ally));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(setup.recipientPartyId, out var surrendered));

            Assert.Same(mapEvent.DefenderSide, ally.Party.MapEventSide);
            Assert.Null(surrendered.Party.MapEventSide);
        });
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MapEvent>(setup.ctx.MapEventId, out _));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(setup.recipientPartyId, out var surrendered));
                Assert.Null(surrendered.Party.MapEventSide);
            });
        }

        // No side-wide conclusion went out — only the surrenderer's own removal broadcast.
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkClosePvpEncounter>());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>());
    }

    /// <summary>
    /// BR-001's exclusivity extended to surrender: while a live mission (or simulation) owns the map event,
    /// a surrender request must not conclude the battle under the players resolving it — the server refuses
    /// it authoritatively (<see cref="ServerBattleModeArbiter"/> backstop; the client menu refusal is
    /// <c>BattleModeEncounterOptionsPatch</c>). Releasing the claim — the state a mission's end broadcast
    /// leaves behind — re-admits the surrender, which then resolves normally.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-001")]
    [Trait("Requirement", "BR-060")]
    public void RecipientSurrender_WhileEventClaimed_IsRefusedUntilClaimReleases()
    {
        var setup = SetupTwoOpposingPlayersInBattle();

        Server.Call(() => Assert.True(ServerBattleModeArbiter.TryClaimMission(setup.ctx.MapEventId)));
        try
        {
            Server.NetworkSentMessages.Clear();

            PublishRecipientSurrender(setup);

            // Refused: nobody was captured, the event survives everywhere, and no encounter close was sent.
            AssertCaptivity(Server, setup.recipientHeroId, null);
            Server.Call(() => Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(setup.ctx.MapEventId, out _)));
            foreach (var client in Clients)
                client.Call(() => Assert.True(client.ObjectManager.TryGetObject<MapEvent>(setup.ctx.MapEventId, out _)));
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkClosePvpEncounter>());
        }
        finally
        {
            Server.Call(() => ServerBattleModeArbiter.Release(setup.ctx.MapEventId));
        }

        // The released claim re-admits the surrender, which resolves the battle as usual.
        PublishRecipientSurrender(setup);

        AssertCaptivity(Server, setup.recipientHeroId, setup.initiatorPartyId);
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkClosePvpEncounter>());
    }

    // ------------------------------------------------------------------
    // Surrender drivers
    // ------------------------------------------------------------------

    /// <summary>Invokes the real (Harmony-patched) <c>PlayerEncounter.PlayerSurrenderInternal</c> — the menu
    /// surrender action — so the coop surrender prefix fires exactly as it does for a live surrender.</summary>
    private static void InvokePatchedSurrender(PlayerEncounter encounter)
    {
        var method = AccessTools.Method(typeof(PlayerEncounter), "PlayerSurrenderInternal");
        Assert.NotNull(method);
        method.Invoke(encounter, new object[method.GetParameters().Length]);
    }

    /// <summary>Publishes the recipient's surrender directly (the client-local <see cref="PlayerSurrendered"/>
    /// that the patched action would raise), for scenarios that assert the resolution rather than the
    /// request path.</summary>
    private void PublishRecipientSurrender(SurrenderSetup setup)
    {
        var recipientClient = Clients.Last();
        recipientClient.Call(() =>
        {
            Assert.True(recipientClient.ObjectManager.TryGetObject<MapEvent>(setup.ctx.MapEventId, out var mapEvent));
            recipientClient.Resolve<IMessageBroker>().Publish(this, new PlayerSurrendered(mapEvent, MobileParty.MainParty));
        }, BattleMenuSurrenderDisabledMethods());
    }

    // ------------------------------------------------------------------
    // Setup / assertions
    // ------------------------------------------------------------------

    private readonly struct SurrenderSetup
    {
        public readonly MapEventContext ctx;
        public readonly string initiatorHeroId;
        public readonly string recipientHeroId;
        public readonly string initiatorPartyId;
        public readonly string recipientPartyId;

        public SurrenderSetup(MapEventContext ctx, string initiatorHeroId, string recipientHeroId,
            string initiatorPartyId, string recipientPartyId)
        {
            this.ctx = ctx;
            this.initiatorHeroId = initiatorHeroId;
            this.recipientHeroId = recipientHeroId;
            this.initiatorPartyId = initiatorPartyId;
            this.recipientPartyId = recipientPartyId;
        }
    }

    /// <summary>
    /// Builds a shared battle with two opposing player parties (attacker = initiator, defender = recipient),
    /// registers both as players, makes each client's <see cref="MobileParty.MainParty"/> its own party, and
    /// seeds the recipient hero so a surrender can capture it. Mirrors <c>CoopBattleFinalizeTests</c>'s
    /// opposing-players setup.
    /// </summary>
    private SurrenderSetup SetupTwoOpposingPlayersInBattle()
    {
        var ctx = CreateServerMapEvent();
        var initiatorHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var recipientHeroId = TestEnvironment.CreateRegisteredObject<Hero>();

        RegisterAsPlayerParty("1", initiatorHeroId, ctx.AttackerPartyId);
        RegisterAsPlayerParty("2", recipientHeroId, ctx.DefenderPartyId);
        PreparePlayerPartyForCapture(recipientHeroId, ctx.DefenderPartyId);

        SetMainPartyInBattle(Clients.First(), ctx.AttackerPartyId);
        SetMainPartyInBattle(Clients.Last(), ctx.DefenderPartyId);
        EnableHeadlessEncounterFinish(Clients.First());
        EnableHeadlessEncounterFinish(Clients.Last());

        return new SurrenderSetup(ctx, initiatorHeroId, recipientHeroId, ctx.AttackerPartyId, ctx.DefenderPartyId);
    }

    /// <summary>
    /// Seeds a companion hero into the member roster of the party with <paramref name="partyId"/>, riding
    /// alongside the party's own (leader) hero. Mirrors <see cref="PreparePlayerPartyForCapture"/>:
    /// AddToCounts replicates the roster element to every client even under AllowedThread
    /// (TroopRosterAddToCountsPatch publishes for AddToCounts regardless), while the PartyBelongedTo wire
    /// stays server-local — exactly the shape of a real companion, whose PartyBelongedTo IS the player party
    /// (which is what makes the TakePrisonerAction postfix re-publish PrisonerTaken for that party).
    /// </summary>
    private void SeedCompanionInParty(string heroId, string partyId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
                hero.PartyBelongedTo = party;
            }
        }, MapEventDisabledMethods);
    }

    private void PreparePlayerPartyForCapture(string heroId, string partyId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(hero.CharacterObject, 1);
                hero.PartyBelongedTo = party;
            }
        }, MapEventDisabledMethods);
    }

    /// <summary>Makes <paramref name="partyId"/> the client's <see cref="MobileParty.MainParty"/> and asserts
    /// it is currently in the battle. Runs in the client's static scope, where <c>Campaign.Current</c> resolves
    /// to that client.</summary>
    private void SetMainPartyInBattle(EnvironmentInstance client, string partyId)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;
            Assert.Same(party, MobileParty.MainParty);
            Assert.NotNull(MobileParty.MainParty.Party.MapEventSide);
        }, MapEventDisabledMethods);
    }

    private static void AssertMainPartyLeftBattle(EnvironmentInstance client)
    {
        client.Call(() => Assert.Null(MobileParty.MainParty.Party.MapEventSide));
    }

    private static void AssertMapEventRemoved(EnvironmentInstance instance, string mapEventId)
    {
        instance.Call(() =>
            Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _),
                $"MapEvent {mapEventId} should be finalized/removed on {instance.GetType().Name}"));
    }

    private static void AssertCaptorPrisonerCount(EnvironmentInstance instance, string captorPartyId, string troopCharacterId, int expected)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captor));
            Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopCharacterId, out var troop));
            Assert.Equal(expected, captor.PrisonRoster.GetElementNumber(troop));
        });
    }

    private IReadOnlyList<MethodBase> BattleMenuSurrenderDisabledMethods()
        => MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "CalculateMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "MovePartyToSuitablePositionOnMapEventFinalize"))
            .ToList();
}
