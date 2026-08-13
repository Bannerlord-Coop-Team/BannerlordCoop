using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Party.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

public class PartyDoneLogicDeathRaceTests : IDisposable
{
    private static readonly MethodBase CreateObituaryMethod =
        AccessTools.Method(typeof(KillCharacterAction), "CreateObituary");

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public PartyDoneLogicDeathRaceTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    [Fact]
    public void DelayedPartyDone_AfterHeroRemoved_DropsHeroCaptureAndAppliesOrdinaryPrisoner()
    {
        string partyId = null;
        string targetHeroId = null;
        string targetCharacterId = null;
        string ordinaryTroopId = null;

        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var targetHero = GameObjectCreator.CreateInitializedObject<Hero>();
            var ordinaryTroop = GameObjectCreator.CreateInitializedObject<CharacterObject>();

            // Keep ApplyByRemove focused on the hero lifecycle rather than clan succession.
            targetHero._clan = null;

            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(Server.ObjectManager.TryGetId(targetHero, out targetHeroId));
            Assert.True(Server.ObjectManager.TryGetId(targetHero.CharacterObject, out targetCharacterId));
            Assert.True(Server.ObjectManager.TryGetId(ordinaryTroop, out ordinaryTroopId));
        });
        TestEnvironment.FlushCoalescer();

        PartyDoneLogicAttempted delayedMessage = default;
        var requestingClient = Clients.First();
        requestingClient.Call(() =>
        {
            Assert.True(requestingClient.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(requestingClient.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.True(requestingClient.ObjectManager.TryGetObject<CharacterObject>(ordinaryTroopId, out var ordinaryTroop));

            var currentPrisoners = new TroopRoster();
            currentPrisoners.AddToCounts(targetHero.CharacterObject, 1);
            currentPrisoners.AddToCounts(ordinaryTroop, 1);

            var takenPrisoners = new FlattenedTroopRoster(4);
            takenPrisoners.Add(targetHero.CharacterObject, 1, 0);
            takenPrisoners.Add(ordinaryTroop, 1, 0);

            delayedMessage = new PartyDoneLogicAttempted(
                party.LeaderHero,
                new FlattenedTroopRoster(4),
                takenPrisoners,
                new FlattenedTroopRoster(4),
                leftMemberRoster: null,
                leftPrisonerRoster: null,
                rightMemberRoster: null,
                rightPrisonerRoster: currentPrisoners,
                initialLeftMemberRoster: null,
                initialLeftPrisonerRoster: null,
                initialRightMemberRoster: null,
                initialRightPrisonerRoster: new TroopRoster(),
                party.ItemRoster,
                new List<Tuple<CharacterObject, CharacterObject, int>>(),
                leftParty: null,
                partyGoldChangeAmount: 0,
                partyInfluenceChangeAmount: 0,
                partyMoraleChangeAmount: 0,
                doNotApplyGoldTransactions: true,
                Helpers.PartyScreenHelper.PartyScreenMode.Normal,
                applyReleasedAndTakenPrisonerActions: true);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));

            KillCharacterAction.ApplyByRemove(targetHero);

            Assert.Equal(Hero.CharacterStates.Dead, targetHero.HeroState);
            Assert.Equal(KillCharacterAction.KillCharacterActionDetail.Lost, targetHero.DeathMark);
        }, new[] { CreateObituaryMethod });
        TestEnvironment.FlushCoalescer();

        requestingClient.Call(() => requestingClient.SimulateMessage(this, delayedMessage));
        TestEnvironment.FlushCoalescer();

        AssertFixedState(Server, partyId, targetHeroId, targetCharacterId, ordinaryTroopId, assertDeathState: true);
        foreach (var client in Clients)
            AssertFixedState(client, partyId, targetHeroId, targetCharacterId, ordinaryTroopId, assertDeathState: false);
    }

    private static void AssertFixedState(
        EnvironmentInstance instance,
        string partyId,
        string targetHeroId,
        string targetCharacterId,
        string ordinaryTroopId,
        bool assertDeathState)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(targetCharacterId, out var targetCharacter));
            Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(ordinaryTroopId, out var ordinaryTroop));

            if (assertDeathState)
            {
                Assert.Equal(Hero.CharacterStates.Dead, targetHero.HeroState);
                Assert.Equal(KillCharacterAction.KillCharacterActionDetail.Lost, targetHero.DeathMark);
            }
            Assert.Null(targetHero.PartyBelongedToAsPrisoner);
            Assert.Equal(0, party.PrisonRoster.GetTroopCount(targetCharacter));
            Assert.Equal(1, party.PrisonRoster.GetTroopCount(ordinaryTroop));
        });
    }

    public void Dispose() => TestEnvironment.Dispose();
}
