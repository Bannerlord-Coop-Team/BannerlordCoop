using Common;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.MapEvents.Interfaces;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class FreeOrCapturePrisonerHeroesTests : IDisposable
{
    private static readonly List<CharacterObject> openedConversations = new();
    private static Hero? oneToOneConversationHero;

    private readonly E2ETestEnvironment testEnvironment;
    private EnvironmentInstance Server => testEnvironment.Server;
    private IReadOnlyList<EnvironmentInstance> Clients => testEnvironment.Clients.ToList();

    public FreeOrCapturePrisonerHeroesTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
    }

    [Fact]
    public void DoFreeOrCapturePrisonerHeroes_ForeignPlayerCompanion_ReleasesWithoutConversation()
    {
        var fixture = CreateFixture();
        var companionId = CreateCaptiveHero(fixture.OwnerPartyId, fixture.CaptorPartyId, Occupation.Wanderer);
        var rescuer = Clients[0];
        rescuer.NetworkSentMessages.Clear();
        Server.InternalMessages.Clear();

        RunFreeHeroes(rescuer, fixture, new[] { companionId }, encounter =>
        {
            encounter.DoFreeOrCapturePrisonerHeroes();

            Assert.Empty(openedConversations);
            Assert.Empty(encounter._capturedAlreadyPrisonerHeroes);
            Assert.Equal(PlayerEncounterState.LootParty, encounter.EncounterState);
        });

        var release = Assert.Single(rescuer.NetworkSentMessages.OfType<NetworkEndCaptivityAttempted>());
        Assert.Equal(companionId, release.PrisonerId);
        Assert.Equal(EndCaptivityDetail.ReleasedAfterBattle, release.Detail);
        testEnvironment.FlushCoalescer();
        AssertReleasedOnceEverywhere(companionId, fixture.CaptorPartyId);
    }

    [Fact]
    public void UpdateInternalAfterBattle_ForeignPlayerCompanion_ReleasesOnce()
    {
        var fixture = CreateFixture();
        var companionId = CreateCaptiveHero(fixture.OwnerPartyId, fixture.CaptorPartyId, Occupation.Wanderer);
        var rescuer = Clients[0];
        rescuer.NetworkSentMessages.Clear();
        Server.InternalMessages.Clear();

        RunFreeHeroes(rescuer, fixture, new[] { companionId }, encounter =>
        {
            new PlayerEncounterInterface().UpdateInternalAfterBattle(encounter);

            Assert.Empty(openedConversations);
            Assert.Equal(PlayerEncounterState.LootParty, encounter.EncounterState);
        });

        var release = Assert.Single(rescuer.NetworkSentMessages.OfType<NetworkEndCaptivityAttempted>());
        Assert.Equal(companionId, release.PrisonerId);
        testEnvironment.FlushCoalescer();
        AssertReleasedOnceEverywhere(companionId, fixture.CaptorPartyId);
    }

    [Fact]
    public void DoFreeOrCapturePrisonerHeroes_UnregisteredForeignCompanion_IsDequeuedWithoutConversation()
    {
        var fixture = CreateFixture();
        var rescuer = Clients[0];
        rescuer.NetworkSentMessages.Clear();

        RunFreeHeroes(rescuer, fixture, Array.Empty<string>(), encounter =>
        {
            Assert.True(rescuer.ObjectManager.TryGetObject<MobileParty>(fixture.OwnerPartyId, out var ownerParty));
            Assert.True(rescuer.ObjectManager.TryGetObject<MobileParty>(fixture.CaptorPartyId, out var captorParty));
            Hero companion;
            // Created with patches off, so the release cannot be forwarded.
            using (new AllowedThread())
            {
                companion = GameObjectCreator.CreateInitializedObject<Hero>();
                companion._companionOf = ownerParty.ActualClan;
                companion._heroState = Hero.CharacterStates.Prisoner;
                companion.PartyBelongedToAsPrisoner = captorParty.Party;
            }
            Assert.False(rescuer.ObjectManager.TryGetId(companion, out _));
            encounter.RosterToReceiveLootMembers.AddToCounts(companion.CharacterObject, 1);

            encounter.DoFreeOrCapturePrisonerHeroes();

            Assert.Empty(openedConversations);
            Assert.Equal(PlayerEncounterState.LootParty, encounter.EncounterState);
        });

        Assert.Empty(rescuer.NetworkSentMessages.OfType<NetworkEndCaptivityAttempted>());
    }

    [Fact]
    public void DoFreeOrCapturePrisonerHeroes_OwnCompanion_StillOpensRescueConversation()
    {
        var fixture = CreateFixture();
        var companionId = CreateCaptiveHero(fixture.RescuerPartyId, fixture.CaptorPartyId, Occupation.Wanderer);
        var rescuer = Clients[0];
        rescuer.NetworkSentMessages.Clear();

        RunFreeHeroes(rescuer, fixture, new[] { companionId }, encounter =>
        {
            encounter.DoFreeOrCapturePrisonerHeroes();

            Assert.True(rescuer.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.Same(companion.CharacterObject, Assert.Single(openedConversations));
            Assert.True(companion.IsPrisoner);
            Assert.True(encounter._stateHandled);
            Assert.Equal(PlayerEncounterState.FreeHeroes, encounter.EncounterState);
        });

        Assert.Empty(rescuer.NetworkSentMessages.OfType<NetworkEndCaptivityAttempted>());
    }

    [Fact]
    public void DoFreeOrCapturePrisonerHeroes_MixedQueue_ReleasesForeignThenOneConversation()
    {
        var fixture = CreateFixture();
        var companionId = CreateCaptiveHero(fixture.OwnerPartyId, fixture.CaptorPartyId, Occupation.Wanderer);
        var ownerHeroId = CreateCaptiveHero(fixture.OwnerPartyId, fixture.CaptorPartyId, Occupation.Lord, asPartyLeader: true);
        var lordId = CreateCaptiveHero(null, fixture.CaptorPartyId, Occupation.Lord);
        var rescuer = Clients[0];
        rescuer.NetworkSentMessages.Clear();
        var harmony = new Harmony($"{nameof(FreeOrCapturePrisonerHeroesTests)}.{Guid.NewGuid():N}");

        try
        {
            RunFreeHeroes(rescuer, fixture, new[] { companionId, lordId, ownerHeroId }, encounter =>
            {
                Assert.True(rescuer.ObjectManager.TryGetObject<Hero>(lordId, out var lord));

                encounter.DoFreeOrCapturePrisonerHeroes();

                Assert.Same(lord.CharacterObject, Assert.Single(openedConversations));
                Assert.Same(lord.CharacterObject, Assert.Single(encounter._capturedAlreadyPrisonerHeroes).Character);

                // The #2584 release consequence dequeues the lord from the list the prefix built.
                oneToOneConversationHero = lord;
                harmony.Patch(
                    AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.OneToOneConversationHero)),
                    prefix: new HarmonyMethod(AccessTools.Method(
                        typeof(FreeOrCapturePrisonerHeroesTests), nameof(GetOneToOneConversationHeroPrefix))));
                Assert.False(LordConversationsCampaignBehaviorPatches
                    .ConversationTalkLordFreedToLordReleaseOnConsequencePrefix());
                harmony.UnpatchAll(harmony.Id);

                encounter._stateHandled = false;
                encounter.DoFreeOrCapturePrisonerHeroes();

                Assert.Single(openedConversations);
                Assert.Equal(PlayerEncounterState.LootParty, encounter.EncounterState);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            oneToOneConversationHero = null;
        }

        var releasedIds = rescuer.NetworkSentMessages.OfType<NetworkEndCaptivityAttempted>()
            .Select(message => message.PrisonerId)
            .OrderBy(id => id)
            .ToArray();
        Assert.Equal(new[] { companionId, ownerHeroId }.OrderBy(id => id), releasedIds);
    }

    [Fact]
    public void TwoWinners_SameCompanion_ReleasedOnce()
    {
        var fixture = CreateFixture();
        var companionId = CreateCaptiveHero(fixture.OwnerPartyId, fixture.CaptorPartyId, Occupation.Wanderer);
        var secondRescuerPartyId = CreatePlayerParty("SecondRescuer");
        Clients[1].Call(() => Clients[1].Resolve<IControllerIdProvider>().SetControllerId("SecondRescuer"));
        foreach (var client in Clients)
            client.NetworkSentMessages.Clear();
        Server.InternalMessages.Clear();

        // Both local loot rolls got the companion, and the second winner runs before the release replicates.
        RunFreeHeroes(Clients[0], fixture, new[] { companionId }, encounter =>
        {
            encounter.DoFreeOrCapturePrisonerHeroes();
            Assert.Empty(openedConversations);
        });
        RunFreeHeroes(Clients[1], fixture with { RescuerPartyId = secondRescuerPartyId }, new[] { companionId }, encounter =>
        {
            encounter.DoFreeOrCapturePrisonerHeroes();
            Assert.Empty(openedConversations);
            Assert.Equal(PlayerEncounterState.LootParty, encounter.EncounterState);
        });

        foreach (var client in Clients)
            Assert.Single(client.NetworkSentMessages.OfType<NetworkEndCaptivityAttempted>());
        testEnvironment.FlushCoalescer();
        AssertReleasedOnceEverywhere(companionId, fixture.CaptorPartyId);
    }

    private static bool RecordConversationPrefix(ConversationCharacterData conversationPartnerData)
    {
        openedConversations.Add(conversationPartnerData.Character);
        return false;
    }

    private static bool GetOneToOneConversationHeroPrefix(ref Hero? __result)
    {
        __result = oneToOneConversationHero;
        return false;
    }

    private static bool StopAtLootPrefix(PlayerEncounter __instance)
    {
        __instance._stateHandled = true;
        return false;
    }

    private static void RunFreeHeroes(EnvironmentInstance client, FreeHeroesFixture fixture,
        IEnumerable<string> lootHeroIds, Action<PlayerEncounter> act)
    {
        var harmony = new Harmony($"{nameof(FreeOrCapturePrisonerHeroesTests)}.{Guid.NewGuid():N}");

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(fixture.RescuerPartyId, out var rescuerParty));
            var previousMainParty = Campaign.Current.MainParty;
            var previousFaction = Campaign.Current.PlayerDefaultFaction;
            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter.EncounterState = PlayerEncounterState.FreeHeroes;
            foreach (var heroId in lootHeroIds)
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(hero.IsPrisoner);
                encounter.RosterToReceiveLootMembers.AddToCounts(hero.CharacterObject, 1);
            }

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = rescuerParty;
                Campaign.Current.PlayerDefaultFaction = rescuerParty.ActualClan;
            }
            Campaign.Current.PlayerEncounter = encounter;

            try
            {
                // CampaignMapConversation.OpenConversation is small enough to be inlined, so record one level down.
                harmony.Patch(
                    AccessTools.Method(typeof(ConversationManager), nameof(ConversationManager.OpenMapConversation)),
                    prefix: new HarmonyMethod(AccessTools.Method(
                        typeof(FreeOrCapturePrisonerHeroesTests), nameof(RecordConversationPrefix))));
                harmony.Patch(
                    AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.DoLootMembersAndPrisonersOfParty)),
                    prefix: new HarmonyMethod(AccessTools.Method(
                        typeof(FreeOrCapturePrisonerHeroesTests), nameof(StopAtLootPrefix))));

                act(encounter);
            }
            finally
            {
                harmony.UnpatchAll(harmony.Id);
                openedConversations.Clear();
                Campaign.Current.PlayerEncounter = null;
                using (new AllowedThread())
                {
                    Campaign.Current.MainParty = previousMainParty;
                    Campaign.Current.PlayerDefaultFaction = previousFaction;
                }
            }
        });
    }

    private FreeHeroesFixture CreateFixture()
    {
        string? captorPartyId = null;
        Server.Call(() =>
        {
            var captorParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(captorParty, out captorPartyId));
        });
        testEnvironment.FlushCoalescer();

        var rescuerPartyId = CreatePlayerParty("Rescuer");
        var ownerPartyId = CreatePlayerParty("CompanionOwner");
        Clients[0].Call(() => Clients[0].Resolve<IControllerIdProvider>().SetControllerId("Rescuer"));
        Clients[1].Call(() => Clients[1].Resolve<IControllerIdProvider>().SetControllerId("CompanionOwner"));

        return new FreeHeroesFixture(rescuerPartyId, ownerPartyId, captorPartyId!);
    }

    // Registers the party leader as a player on every instance without a peer, like an offline player.
    private string CreatePlayerParty(string controllerId)
    {
        string? partyId = null;
        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
        });
        testEnvironment.FlushCoalescer();

        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId!, out var party));
                Assert.True(instance.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
                Assert.True(instance.ObjectManager.TryGetId(party.ActualClan, out var clanId));
                Assert.True(instance.ObjectManager.TryGetId(party.LeaderHero.CharacterObject, out var characterId));
                Assert.True(instance.Resolve<IPlayerManager>().AddPlayer(
                    new Player(controllerId, heroId, partyId!, clanId, characterId)));
            });
        }

        return partyId!;
    }

    private string CreateCaptiveHero(string? clanPartyId, string captorPartyId, Occupation occupation,
        bool asPartyLeader = false)
    {
        string? heroId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captorParty));
            Hero hero;
            if (asPartyLeader)
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(clanPartyId!, out var leaderParty));
                hero = leaderParty.LeaderHero;
            }
            else
            {
                hero = GameObjectCreator.CreateInitializedObject<Hero>();
                hero.SetNewOccupation(occupation);
                if (clanPartyId != null)
                {
                    Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(clanPartyId, out var clanParty));
                    hero.Clan = null;
                    AddCompanionAction.Apply(clanParty.ActualClan, hero);
                }
            }

            TakePrisonerAction.Apply(captorParty.Party, hero);
            Assert.True(hero.IsPrisoner);
            Assert.True(Server.ObjectManager.TryGetId(hero, out heroId));
        });
        testEnvironment.FlushCoalescer();

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId!, out var hero));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captorParty));
                Assert.Same(captorParty.Party, hero.PartyBelongedToAsPrisoner);

                // The hero state relay lives in Coop.Core, which E2E does not run, so mirror it here.
                using (new AllowedThread())
                {
                    hero._heroState = Hero.CharacterStates.Prisoner;
                }
            });
        }

        return heroId!;
    }

    private void AssertReleasedOnceEverywhere(string heroId, string captorPartyId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captor));
            Assert.False(hero.IsPrisoner);
            Assert.Single(Server.InternalMessages.OfType<CountsAtIndexAdded>(),
                message => message.TroopRoster == captor.PrisonRoster &&
                    message.Character == hero.CharacterObject && message.CountChange == -1);
        });

        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captor));
                Assert.Equal(0, captor.PrisonRoster.GetTroopCount(hero.CharacterObject));
            });
        }
    }

    private readonly record struct FreeHeroesFixture(
        string RescuerPartyId,
        string OwnerPartyId,
        string CaptorPartyId);

    public void Dispose() => testEnvironment.Dispose();
}
