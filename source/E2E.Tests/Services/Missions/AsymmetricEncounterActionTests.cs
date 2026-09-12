using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// BR-072 (Asymmetric Player Actions): "The battle shall resolve correctly when different players choose
/// different valid encounter actions. For example, if one side attacks and the opposing side surrenders,
/// the battle shall resolve as a surrender without requiring combat."
/// <para>
/// The headline attack-vs-surrender resolution is exercised by <c>CoopBattleFinalizeTests</c>
/// (RecipientSurrenders_*), but those tests do not frame it as a BR-072 resolution-correctness case — they
/// never assert the "without requiring combat" clause. This test drives the same asymmetry (one player is the
/// aggressor; the opposing player surrenders) and asserts the outcome is the surrender consequence with NO
/// battle mission ever started: the aggressor is never taken captive, the surrendering player is, and the
/// shared map event is torn down everywhere.
/// </para>
/// </summary>
public class AsymmetricEncounterActionTests : MapEventTestBase
{
    public AsymmetricEncounterActionTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Two opposing players in one battle pick different valid encounter actions: the initiator attacks, the
    /// recipient surrenders. The battle must resolve as the surrender — the recipient becomes the initiator's
    /// prisoner and the shared event is finalized everywhere — without any combat: no battle-mission start is
    /// ever broadcast, and the aggressor is not itself captured.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-072")]
    public void AttackVsSurrender_ResolvesAsSurrender_WithoutRequiringCombat()
    {
        var setup = SetupTwoOpposingPlayersInBattle();

        // Both players sit in a local encounter at the battle's encounter menu when the surrender resolves.
        SetMockPlayerEncounter(Clients.First());
        SetMockPlayerEncounter(Clients.Last());

        // "without requiring combat": no battle mission may be started on any instance to resolve this battle.
        var missionStarts = new List<string>();
        foreach (var instance in AllInstances())
            instance.Resolve<IMessageBroker>().Subscribe<NetworkStartAttackMission>(p => missionStarts.Add(p.What.MapEventId));

        // The initiator chose to attack (it is the aggressor holding the encounter). The opposing player chooses
        // the surrender action instead. Drive the recipient's surrender through its real client path.
        var recipientClient = Clients.Last();
        recipientClient.Call(() =>
        {
            Assert.True(recipientClient.ObjectManager.TryGetObject<MapEvent>(setup.mapEventId, out var mapEvent));
            recipientClient.Resolve<IMessageBroker>().Publish(this, new PlayerSurrendered(mapEvent, MobileParty.MainParty));
        }, SurrenderResolutionDisabledMethods());

        // The battle resolved everywhere — the shared event is gone from the server and both players' clients.
        AssertMapEventRemoved(Server, setup.mapEventId);
        foreach (var client in Clients)
            AssertMapEventRemoved(client, setup.mapEventId);

        // It resolved AS a surrender: the surrendering player is the aggressor's captive, replicated to all.
        AssertCaptivity(Server, setup.recipientHeroId, setup.initiatorPartyId);
        foreach (var client in Clients)
            AssertCaptivity(client, setup.recipientHeroId, setup.initiatorPartyId);

        // "without requiring combat": the opposing (attacking) side never had to enter or fight a battle
        // mission to resolve this, and the aggressor is not itself captured/harmed by the resolution.
        Assert.Empty(missionStarts);
        AssertCaptivity(Server, setup.initiatorHeroId, null);
        foreach (var client in Clients)
            AssertCaptivity(client, setup.initiatorHeroId, null);
    }

    // ------------------------------------------------------------------
    // Setup / helpers (mirrors the private scaffolding of CoopBattleFinalizeTests, kept local so this file
    // owns everything it needs — the sibling helpers are private to that test class).
    // ------------------------------------------------------------------

    private IEnumerable<EnvironmentInstance> AllInstances()
    {
        yield return Server;
        foreach (var client in Clients)
            yield return client;
    }

    /// <summary>
    /// Builds a shared battle with two opposing player parties, registers both as players, makes each client's
    /// <see cref="MobileParty.MainParty"/> its own party, and seeds the defender hero so surrender can capture it.
    /// </summary>
    private (string mapEventId, string initiatorHeroId, string recipientHeroId, string initiatorPartyId, string recipientPartyId)
        SetupTwoOpposingPlayersInBattle()
    {
        var ctx = CreateServerMapEvent();
        var initiatorHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var recipientHeroId = TestEnvironment.CreateRegisteredObject<Hero>();

        RegisterAsPlayerParty("initiator", initiatorHeroId, ctx.AttackerPartyId);
        RegisterAsPlayerParty("recipient", recipientHeroId, ctx.DefenderPartyId);
        PreparePlayerPartyForCapture(recipientHeroId, ctx.DefenderPartyId);

        SetMainPartyInBattle(Clients.First(), ctx.AttackerPartyId);
        SetMainPartyInBattle(Clients.Last(), ctx.DefenderPartyId);
        EnableHeadlessEncounterFinish(Clients.First());
        EnableHeadlessEncounterFinish(Clients.Last());

        return (ctx.MapEventId, initiatorHeroId, recipientHeroId, ctx.AttackerPartyId, ctx.DefenderPartyId);
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

    /// <summary>Makes <paramref name="partyId"/> the client's <see cref="MobileParty.MainParty"/> and asserts it
    /// is currently in the battle. Runs in the client's static scope, where <c>Campaign.Current</c> resolves to
    /// that client.</summary>
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

    /// <summary>The world-dependent loot/result/finalize steps need a live campaign scene, so disable them; the
    /// surrender still resolves the capture + teardown. <c>GameMenu.ExitToLast</c> (no menu context headless) is
    /// disabled so the encounter-close fallback does not deref a null menu context.</summary>
    private IReadOnlyList<MethodBase> SurrenderResolutionDisabledMethods()
        => MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "CalculateMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "MovePartyToSuitablePositionOnMapEventFinalize"))
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .ToList();

    private static void AssertMapEventRemoved(EnvironmentInstance instance, string mapEventId)
    {
        instance.Call(() =>
            Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _),
                $"MapEvent {mapEventId} should be finalized/removed on {instance.GetType().Name}"));
    }
}
