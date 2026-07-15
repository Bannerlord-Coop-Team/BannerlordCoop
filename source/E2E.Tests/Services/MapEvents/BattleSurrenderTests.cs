using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
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
/// <item>BR-061 — the final result records the surrendered hero as a prisoner (and the "troops as prisoners"
/// clause, which the current player-party forfeiture does not satisfy, is pinned as a TDD-red skip).</item>
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
