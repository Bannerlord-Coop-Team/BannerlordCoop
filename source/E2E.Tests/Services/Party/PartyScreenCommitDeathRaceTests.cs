using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using Common.Network;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
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

public class PartyScreenCommitDeathRaceTests : IDisposable
{
    private static readonly MethodBase CreateObituaryMethod =
        AccessTools.Method(typeof(KillCharacterAction), "CreateObituary");

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public PartyScreenCommitDeathRaceTests(ITestOutputHelper output)
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
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var leaderHeroId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("RequestingControllerId", leaderHeroId, string.Empty, string.Empty, string.Empty)));
            playerManager.SetPeer("RequestingControllerId", Clients.First().NetPeer);
        });
        TestEnvironment.FlushCoalescer();

        uint targetHandle = Server.GetHandle<CharacterObject>(targetCharacterId);
        uint ordinaryHandle = Server.GetHandle<CharacterObject>(ordinaryTroopId);
        NetworkCommitPartyScreen delayedMessage = default;
        var requestingClient = Clients.First();
        requestingClient.Call(() =>
        {
            Assert.True(requestingClient.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(requestingClient.ObjectManager.TryGetId(party.LeaderHero, out var leaderHeroId));

            // A loot screen that offered both prisoners, and the player took both.
            var lootPrisoners = new TroopRosterData(new[]
            {
                new TroopRosterElementData(targetHandle, 1, 0, 0),
                new TroopRosterElementData(ordinaryHandle, 1, 0, 0),
            });

            delayedMessage = PartyScreenCommits.Create(
                leaderHeroId,
                Helpers.PartyScreenHelper.PartyScreenMode.Loot,
                PartyScreenDoneKind.Default,
                new[] { Take(targetHandle), Take(ordinaryHandle) },
                leftPrisonerSeed: lootPrisoners);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));

            KillCharacterAction.ApplyByRemove(targetHero);

            Assert.Equal(Hero.CharacterStates.Dead, targetHero.HeroState);
            Assert.Equal(KillCharacterAction.KillCharacterActionDetail.Lost, targetHero.DeathMark);
        }, new[] { CreateObituaryMethod });
        TestEnvironment.FlushCoalescer();

        requestingClient.Call(() => requestingClient.Resolve<INetwork>().SendAll(delayedMessage));
        TestEnvironment.FlushCoalescer();

        AssertFixedState(Server, partyId, targetHeroId, targetCharacterId, ordinaryTroopId, assertDeathState: true);
        foreach (var client in Clients)
            AssertFixedState(client, partyId, targetHeroId, targetCharacterId, ordinaryTroopId, assertDeathState: false);
    }

    private static PartyScreenCommandData Take(uint characterHandle)
        => PartyScreenCommits.Transfer(PartyScreenLogic.PartyRosterSide.Left, PartyScreenLogic.TroopType.Prisoner, characterHandle, 1);

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
