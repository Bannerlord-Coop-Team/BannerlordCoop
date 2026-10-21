using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;
using Behavior = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest;

namespace E2E.Tests.Services.Issues;

public class HeadmanNeedsGrainTests : IDisposable
{
    private static readonly MethodBase[] Texts =
    {
        AccessTools.Method(typeof(Quest), nameof(Quest.SetDialogs)),
        AccessTools.Method(typeof(LogEntry), nameof(LogEntry.AddLogEntry), new[] { typeof(LogEntry) }),
    };

    private readonly E2ETestEnvironment environment;
    private readonly Harmony textHarmony = new($"grain-text-{Guid.NewGuid()}");
    private readonly QuestScenario scenario;
    private readonly string villageSettlementId;
    private readonly string villageId;
    private readonly string townSettlementId;
    private readonly string townId;
    private readonly string kingdomId;
    private readonly string giverClanId;
    private readonly string playerClanId;
    private readonly string otherPlayerClanId;
    private readonly string grainCategoryId;
    private readonly string otherNotableId;
    private EnvironmentInstance Server => scenario.Server;
    private EnvironmentInstance ClientA => scenario.ClientA;
    private EnvironmentInstance ClientB => scenario.ClientB;
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public HeadmanNeedsGrainTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        textHarmony.Patch(AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.EncyclopediaLink)),
            prefix: new HarmonyMethod(typeof(HeadmanNeedsGrainTests), nameof(HeroLinkPrefix)));
        villageSettlementId = environment.CreateRegisteredObject<Settlement>();
        villageId = environment.CreateRegisteredObject<Village>();
        townSettlementId = environment.CreateRegisteredObject<Settlement>();
        townId = environment.CreateRegisteredObject<Town>();
        kingdomId = environment.CreateRegisteredObject<Kingdom>();
        giverClanId = environment.CreateRegisteredObject<Clan>();
        playerClanId = environment.CreateRegisteredObject<Clan>();
        otherPlayerClanId = environment.CreateRegisteredObject<Clan>();
        grainCategoryId = environment.CreateRegisteredObject<ItemCategory>();
        otherNotableId = environment.CreateRegisteredObject<Hero>();
        scenario = new QuestScenario(environment, hero => new Issue(hero), ConfigureGiver);

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var issue = IssueHere(instance);
                    Campaign.Current.PlayerTraitDeveloper = new PropertyOwner<PropertyObject>();
                    issue._nearbySuitableSettlementCache = Get<Settlement>(instance, townSettlementId);
                    foreach (var player in new[] { scenario.PlayerA, scenario.PlayerB })
                    {
                        var hero = Get<Hero>(instance, player.HeroId);
                        hero._partyBelongedTo = Get<MobileParty>(instance, player.PartyId);
                        hero.Clan = Get<Clan>(instance, player.HeroId == scenario.PlayerA.HeroId ? playerClanId : otherPlayerClanId);
                        hero.Clan.SetLeader(hero);
                        hero.Clan.Renown = 0;
                        hero.Gold = 10000;
                    }

                    if (instance != Server)
                    {
                        var player = instance == ClientA ? scenario.PlayerA : scenario.PlayerB;
                        Game.Current.PlayerTroop = Get<Hero>(instance, player.HeroId).CharacterObject;
                        Campaign.Current.MainParty = Get<MobileParty>(instance, player.PartyId);
                        Campaign.Current.PlayerDefaultFaction = Get<Hero>(instance, player.HeroId).Clan;
                    }
                }
                new IssuesCampaignBehavior().RegisterEvents();
            });
        }

        Server.Call(() =>
        {
            foreach (var player in new[] { scenario.PlayerA, scenario.PlayerB })
                Get<MobileParty>(Server, player.PartyId).ItemRoster.Clear();
        });
        environment.FlushCoalescer();
    }

    public void Dispose()
    {
        environment.Dispose();
        textHarmony.UnpatchAll(textHarmony.Id);
    }

    // The managed campaign harness has no encyclopedia page catalog.
    private static bool HeroLinkPrefix(Hero __instance, ref string __result)
    {
        __result = __instance.StringId;
        return false;
    }

    [Fact]
    public void Accept_UsesRealPreconditionsAndKeepsTheQuestOnlyOnTheOwnerClient()
    {
        Accept();
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        var amount = 0;
        ClientA.Call(() => amount = QuestHere(ClientA)._neededGrainAmount);
        Server.Call(() => Assert.Equal(amount, QuestHere(Server)._neededGrainAmount));
        ClientB.Call(() => Assert.Null(IssueHere(ClientB).IssueQuest));
    }

    [Fact]
    public void Accept_DoesNotNormalizeUnrelatedHiddenTraits()
    {
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                using (new AllowedThread()) Get<Hero>(instance, scenario.PlayerA.HeroId).SetTraitLevel(DefaultTraits.Commander, 5);
            });
        ClientA.Call(() => Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Commander, 4000));
        Accept();
        foreach (var instance in Instances)
            instance.Call(() => Assert.Equal(5, Get<Hero>(instance, scenario.PlayerA.HeroId).GetTraitLevel(DefaultTraits.Commander)));
    }

    [Fact]
    public void TraitSeed_KeepsGenerosityUntilItsRewardIsAcknowledged()
    {
        Server.Call(() =>
        {
            var owner = Get<Hero>(Server, scenario.PlayerA.HeroId);
            var rewards = Server.Resolve<IHeadmanNeedsGrainTraitRewards>();
            var model = Campaign.Current.Models.CharacterDevelopmentModel;
            var startingXp = model.GetTraitXpRequiredForTraitLevel(DefaultTraits.Generosity, 1) - 25;
            rewards.Seed(owner, new Dictionary<string, int> { [DefaultTraits.Generosity.StringId] = startingXp });
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
            {
                rewards.Apply(DefaultTraits.Mercy, 70, owner, ActionNotes.QuestSuccess);
                rewards.Apply(DefaultTraits.Generosity, 50, owner, ActionNotes.QuestSuccess);
            }
            model.GetTraitLevelForTraitXp(owner, DefaultTraits.Generosity, startingXp + 50,
                out var expectedLevel, out var expectedXp);
            rewards.Seed(owner, new Dictionary<string, int>
            {
                [DefaultTraits.Mercy.StringId] = 70,
                [DefaultTraits.Generosity.StringId] = startingXp,
            });
            Assert.Equal(expectedLevel, owner.GetTraitLevel(DefaultTraits.Generosity));
            rewards.Seed(owner, new Dictionary<string, int> { [DefaultTraits.Generosity.StringId] = expectedXp });
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
                rewards.Apply(DefaultTraits.Generosity, 1, owner, ActionNotes.QuestSuccess);
            var reward = Server.NetworkSentMessages.GetMessages<NetworkHeadmanNeedsGrainTraitReward>().Last();
            Assert.Equal(expectedXp + 1, reward.RemainingXp);
        });
    }
    [Fact]
    public void Delivery_PreservesTraitLevelThresholdCrossing()
    {
        var expectedLevel = 0;
        var expectedXp = 0;
        ClientA.Call(() =>
        {
            var model = Campaign.Current.Models.CharacterDevelopmentModel;
            var xp = model.GetTraitXpRequiredForTraitLevel(DefaultTraits.Generosity, 1) - 25;
            Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Generosity, xp);
            model.GetTraitLevelForTraitXp(Hero.MainHero, DefaultTraits.Generosity, xp + 50, out expectedLevel, out expectedXp);
            Assert.True(expectedLevel > 0);
        });
        Accept();
        StageDelivery(true, true);
        ClientA.Call(() => QuestHere(ClientA).Success(), Texts);
        foreach (var instance in Instances)
            instance.Call(() => Assert.Equal(expectedLevel, Get<Hero>(instance, scenario.PlayerA.HeroId).GetTraitLevel(DefaultTraits.Generosity)));
        ClientA.Call(() => Assert.Equal(expectedXp, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Generosity)));
    }

    [Fact]
    public void Accept_RefusesAnIssueWhoseMarketCanNoLongerSupplyTheQuest()
    {
        Server.Call(() =>
        {
            var market = Get<Town>(Server, townId).MarketData;
            market._itemDict[DefaultItems.Grain.ItemCategory] = new ItemData(0, 0, 350, 0);
            IssueHere(Server)._nearbySuitableSettlementCache = Get<Settlement>(Server, townSettlementId);
        });
        Accept(start: false);
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Server.Call(() => Assert.True(IssueHere(Server).IsOngoingWithoutQuest));
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Delivery_RechecksTheVillageAndTheOwnersActualGrain(bool atVillage, bool enoughGrain, bool succeeds)
    {
        Accept();
        var ownerQuest = (Quest)scenario.QuestOf(ClientA);
        StageDelivery(atVillage, enoughGrain);
        ClientA.Call(() => QuestHere(ClientA).Success(), Texts);
        environment.FlushCoalescer();
        Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestBranch>());
        Assert.Equal(succeeds ? 1 : 0, Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>().Count());

        if (!succeeds)
        {
            ClientA.Call(() => Assert.True(QuestHere(ClientA).IsOngoing));
            return;
        }

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.Null(Get<Hero>(instance, scenario.NotableId).Issue);
                Assert.Equal(0, Get<MobileParty>(instance, scenario.PlayerA.PartyId).ItemRoster.GetItemNumber(DefaultItems.Grain));
                Assert.True(Get<Town>(instance, townId).Prosperity == 1050f, $"{instance.GetType().Name} prosperity {Get<Town>(instance, townId).Prosperity}");
                Assert.Equal(10000, Get<Hero>(instance, scenario.PlayerB.HeroId).Gold);
                Assert.Equal(10000, Get<Hero>(instance, scenario.PlayerA.HeroId).Gold);
                Assert.Equal(110f, Get<Hero>(instance, scenario.NotableId).Power);
                Assert.Equal(1f, Get<Clan>(instance, playerClanId).Renown);
                Assert.Equal(5, Get<Hero>(instance, scenario.PlayerA.HeroId).GetRelation(Get<Hero>(instance, scenario.NotableId)));
                Assert.Equal(1, Get<Hero>(instance, scenario.PlayerA.HeroId).GetRelation(Get<Hero>(instance, otherNotableId)));
                Assert.Equal(0, Get<Hero>(instance, scenario.PlayerB.HeroId).GetRelation(Get<Hero>(instance, scenario.NotableId)));
            });
        }
        ClientA.Call(() =>
        {
            Assert.Single(ownerQuest.JournalEntries.Where(log => log.LogText.Value == ownerQuest.SuccessLog.Value));
            Assert.Equal(70, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Mercy));
            Assert.Equal(50, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Generosity));
            ownerQuest.Success();
            Assert.Equal(50, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Generosity));
        }, Texts);
        ClientB.Call(() => Assert.Equal(0, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Generosity)));
        Server.Call(() => Assert.Equal(0, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Generosity)));
    }

    [Fact]
    public void Timeout_AppliesTheWorldPenaltyOnceAndEndsEveryCopy()
    {
        Accept();
        var ownerQuest = (Quest)scenario.QuestOf(ClientA);
        ClientA.Call(() => QuestHere(ClientA).CompleteQuestWithTimeOut(), Texts);
        environment.FlushCoalescer();
        Assert.Equal(IssueOutcome.QuestTimeOut, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.Null(Get<Hero>(instance, scenario.NotableId).Issue);
                Assert.True(Get<Town>(instance, townId).Prosperity == 990f, $"{instance.GetType().Name} prosperity {Get<Town>(instance, townId).Prosperity}");
                Assert.Equal(95f, Get<Hero>(instance, scenario.NotableId).Power);
                Assert.Equal(-5, Get<Hero>(instance, scenario.PlayerA.HeroId).GetRelation(Get<Hero>(instance, scenario.NotableId)));
                Assert.Equal(-3, Get<Hero>(instance, scenario.PlayerA.HeroId).GetRelation(Get<Hero>(instance, otherNotableId)));
            });
        }
        ClientA.Call(() => Assert.Single(ownerQuest.JournalEntries.Where(log => log.LogText.Value == ownerQuest.QuestTimeoutLogText.Value)));
    }

    [Theory]
    [InlineData(20)]
    [InlineData(-20)]
    public void Delivery_PreservesTraitXpEarnedOrLostAfterAcceptance(int delta)
    {
        Accept();

        ClientA.Call(() => TraitLevelingHelper.AddPlayerTraitXPAndLogEntry(
            DefaultTraits.Generosity, delta, ActionNotes.DefaultNote, Hero.MainHero), Texts);
        StageDelivery(true, true);
        ClientA.Call(() => QuestHere(ClientA).Success(), Texts);
        ClientA.Call(() => Assert.Equal(50 + delta, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Generosity)));
        ClientB.Call(() => Assert.Equal(0, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Generosity)));
    }

    [Fact]
    public void RestoredReward_AcknowledgesPendingXpBeforeLaterLocalProgress()
    {
        IHeadmanNeedsGrainTraitRewards serverRewards = null!;
        Server.Call(() =>
        {
            serverRewards = new HeadmanNeedsGrainTraitRewards(
                new Moq.Mock<Common.Network.INetwork>().Object, Server.ObjectManager);
            var owner = Get<Hero>(Server, scenario.PlayerA.HeroId);
            serverRewards.Seed(owner, new Dictionary<string, int> { [DefaultTraits.Generosity.StringId] = 0 });
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
                serverRewards.Apply(DefaultTraits.Generosity, 50, owner, ActionNotes.QuestSuccess);
        });
        var previousSeeds = ClientA.NetworkSentMessages.GetMessages<RequestHeadmanNeedsGrainTraitSeed>().Count();
        ClientA.Call(() =>
        {
            var rewards = ClientA.Resolve<IHeadmanNeedsGrainTraitRewards>();
            var owner = Get<Hero>(ClientA, scenario.PlayerA.HeroId);
            var records = new Dictionary<string, object>
            {
                ["_coop_grain_trait_progress"] = new Dictionary<Hero, Dictionary<string, int>>
                {
                    [owner] = new() { [DefaultTraits.Generosity.StringId] = 50 },
                },
            };
            rewards.SyncData(new TestDataStore(false, records));
        });
        var restoredSeeds = ClientA.NetworkSentMessages.GetMessages<RequestHeadmanNeedsGrainTraitSeed>().ToArray();
        Assert.Equal(previousSeeds + 1, restoredSeeds.Length);
        Server.Call(() => serverRewards.Seed(Get<Hero>(Server, scenario.PlayerA.HeroId), restoredSeeds.Last().Values));
        ClientA.Call(() =>
        {
            Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(DefaultTraits.Generosity, 75);
            ClientA.Resolve<IHeadmanNeedsGrainTraitRewards>().RequestSeed();
        });
        var laterSeed = ClientA.NetworkSentMessages.GetMessages<RequestHeadmanNeedsGrainTraitSeed>().Last();
        Server.Call(() =>
        {
            var owner = Get<Hero>(Server, scenario.PlayerA.HeroId);
            serverRewards.Seed(owner, laterSeed.Values);
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
                serverRewards.Apply(DefaultTraits.Generosity, 25, owner, ActionNotes.QuestSuccess);
            var records = new Dictionary<string, object>();
            serverRewards.SyncData(new TestDataStore(true, records));
            var progress = (Dictionary<Hero, Dictionary<string, int>>)records["_coop_grain_trait_progress"];
            Assert.Equal(100, progress[owner][DefaultTraits.Generosity.StringId]);
        });
    }
    [Fact]
    public void LoadedTraitProgress_IsRestoredAfterSwitchingFromTheSavedHeroToTheLocalPlayer()
    {
        textHarmony.Patch(AccessTools.Method(typeof(ChangePlayerCharacterAction), nameof(ChangePlayerCharacterAction.Apply)),
            prefix: new HarmonyMethod(typeof(HeadmanNeedsGrainTests), nameof(SkipMapCharacterSwitch)) { priority = Priority.First });
        ClientA.Call(() =>
        {
            var first = Get<Hero>(ClientA, scenario.PlayerA.HeroId);
            var second = Get<Hero>(ClientA, scenario.PlayerB.HeroId);
            var records = new Dictionary<string, object>
            {
                ["_coop_grain_trait_progress"] = new Dictionary<Hero, Dictionary<string, int>>
                {
                    [first] = new() { [DefaultTraits.Generosity.StringId] = 123 },
                    [second] = new() { [DefaultTraits.Generosity.StringId] = 456 },
                },
            };
            Game.Current.PlayerTroop = second.CharacterObject;
            var rewards = ClientA.Resolve<IHeadmanNeedsGrainTraitRewards>();
            rewards.SyncData(new TestDataStore(false, records));
            Assert.Equal(456, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Generosity));
            Game.Current.PlayerTroop = first.CharacterObject;
            ChangePlayerCharacterAction.Apply(first);
            Assert.Equal(123, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Generosity));
        }, Texts);
    }

    [Fact]
    public void RemoteTraitReward_ForNonLocalOwner_IsRestoredAfterSwitchingBack()
    {
        textHarmony.Patch(AccessTools.Method(typeof(ChangePlayerCharacterAction), nameof(ChangePlayerCharacterAction.Apply)),
            prefix: new HarmonyMethod(typeof(HeadmanNeedsGrainTests), nameof(SkipMapCharacterSwitch)) { priority = Priority.First });
        ClientA.Call(() =>
        {
            var owner = Get<Hero>(ClientA, scenario.PlayerA.HeroId);
            var local = Get<Hero>(ClientA, scenario.PlayerB.HeroId);
            var trait = DefaultTraits.Generosity;
            var startingXp = 123;
            var amount = 20;
            var records = new Dictionary<string, object>
            {
                ["_coop_grain_trait_progress"] = new Dictionary<Hero, Dictionary<string, int>>
                {
                    [owner] = new() { [trait.StringId] = startingXp },
                },
            };
            Game.Current.PlayerTroop = local.CharacterObject;
            var rewards = ClientA.Resolve<IHeadmanNeedsGrainTraitRewards>();
            rewards.SyncData(new TestDataStore(false, records));
            Campaign.Current.PlayerTraitDeveloper.SetPropertyValue(trait, 999);

            var model = Campaign.Current.Models.CharacterDevelopmentModel;
            model.GetTraitLevelForTraitXp(owner, trait, startingXp + amount, out var level, out var remainingXp);
            ClientA.SimulateMessage(this, new NetworkHeadmanNeedsGrainTraitReward(
                scenario.PlayerA.HeroId, otherNotableId, trait.StringId, amount, (int)ActionNotes.DefaultNote, level, remainingXp));

            Game.Current.PlayerTroop = owner.CharacterObject;
            ChangePlayerCharacterAction.Apply(owner);
            Assert.Equal(remainingXp, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(trait));
        }, Texts);
    }

    private static bool SkipMapCharacterSwitch() => false;

    [Fact]
    public void JoinReady_RefreshesCompanionFundingAfterTheServerCachedItsPrice()
    {
        Server.Call(() => Campaign.Current.GetCampaignBehavior<Behavior>()._averageGrainPriceInCalradia = 18);
        ClientA.Call(() => Campaign.Current.GetCampaignBehavior<Behavior>()._averageGrainPriceInCalradia = 0);
        ClientA.SimulateMessage(this, new GameInterface.Registry.Messages.AllGameObjectsRegistered());
        environment.FlushCoalescer();
        var expected = 0;
        Server.Call(() => expected = IssueHere(Server).AlternativeSolutionNeededGold);
        Assert.True(expected > 0);
        ClientA.Call(() => Assert.Equal(expected, IssueHere(ClientA).AlternativeSolutionNeededGold));
    }
    [Fact]
    public void LateJoin_LoadsAverageGrainPriceWithoutAnIssueCreationPayload()
    {
        var records = new Dictionary<string, object>();
        Server.Call(() =>
        {
            var behavior = Campaign.Current.GetCampaignBehavior<Behavior>();
            behavior._averageGrainPriceInCalradia = 37;
            behavior.SyncData(new TestDataStore(true, records));
        });
        ClientB.Call(() =>
        {
            var behavior = Campaign.Current.GetCampaignBehavior<Behavior>();
            behavior._averageGrainPriceInCalradia = 0;
            behavior.SyncData(new TestDataStore(false, records));
            Assert.Equal(37, behavior._averageGrainPriceInCalradia);
        });
    }
    [Fact]
    public void SaveData_PreservesTheAveragePriceAndTheServersChosenMarket()
    {
        Server.Call(() =>
        {
            var behavior = Campaign.Current.GetCampaignBehavior<Behavior>();
            var issue = IssueHere(Server);
            var market = issue._nearbySuitableSettlementCache;
            var records = new Dictionary<string, object>();
            behavior.SyncData(new TestDataStore(true, records));
            behavior._averageGrainPriceInCalradia = 0;
            issue._nearbySuitableSettlementCache = null;
            behavior.SyncData(new TestDataStore(false, records));
            Assert.Equal(25, behavior._averageGrainPriceInCalradia);
            Assert.Same(market, issue._nearbySuitableSettlementCache);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AiCompletion_PreservesTheAiOutcomeAndRefusesAControlledPlayer(bool player)
    {
        var solverId = player ? scenario.PlayerA.HeroId : environment.CreateRegisteredObject<Hero>();
        Server.Call(() => IssueHere(Server).CompleteIssueWithAiLord(Get<Hero>(Server, solverId)));
        if (player)
        {
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkHeadmanNeedsGrainAiCompleted>());
            Server.Call(() => Assert.True(IssueHere(Server).IsOngoingWithoutQuest));
            return;
        }
        var completion = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkHeadmanNeedsGrainAiCompleted>());
        Assert.Equal(solverId, completion.SolverId);
        foreach (var instance in Instances)
            instance.Call(() => Assert.Null(Get<Hero>(instance, scenario.NotableId).Issue));
    }

    [Fact]
    public void ServerInventoryUpdates_RefreshTheOwnersJournalAndRemoveTheReadyLogWhenGrainIsLost()
    {
        Accept();
        ClientA.Call(() => ClientA.Resolve<IIssueOwnershipRegistry>().Remove(scenario.NotableId));
        StageDelivery(true, true);
        ClientA.Call(() =>
        {
            var quest = QuestHere(ClientA);
            Assert.Equal(quest._neededGrainAmount, quest._playerAcceptedQuestLog.CurrentProgress);
            Assert.NotNull(quest._playerHasNeededGrainsLog);
            Assert.True(ClientA.Resolve<IIssueOwnershipRegistry>().TrySetOwner(
                scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA));
        }, Texts);
        Server.Call(() => Get<MobileParty>(Server, scenario.PlayerA.PartyId).ItemRoster.Clear());
        environment.FlushCoalescer();
        ClientA.Call(() =>
        {
            var quest = QuestHere(ClientA);
            Assert.Equal(0, quest._playerAcceptedQuestLog.CurrentProgress);
            Assert.Null(quest._playerHasNeededGrainsLog);
        }, Texts);
    }

    [Fact]
    public void ServerRaid_CancelsTheQuestWithoutTheClientRequestingAnOutcome()
    {
        Accept();
        Server.Call(() => CampaignEventDispatcher.Instance.OnVillageBeingRaided(Get<Village>(Server, villageId)), Texts);
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
    }

    [Theory]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByRebellion, IssueOutcome.QuestCancel)]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByCrimeRatingChange, IssueOutcome.QuestFail)]
    public void ServerWar_UsesTheOwnersFactionAndDoesNotRequireAnActiveMapEvent(
        DeclareWarAction.DeclareWarDetail detail, IssueOutcome outcome)
    {
        Accept();
        var ownerQuest = (Quest)scenario.QuestOf(ClientA);
        Server.Call(() =>
        {
            var hero = Get<Hero>(Server, scenario.PlayerA.HeroId);
            using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
                DeclareWarAction.ApplyInternal(Get<Kingdom>(Server, kingdomId), Get<Clan>(Server, playerClanId), detail);
        }, Texts);
        Assert.Equal(outcome, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        if (detail == DeclareWarAction.DeclareWarDetail.CausedByCrimeRatingChange)
        {
            ClientA.Call(() =>
            {
                Assert.Single(ownerQuest.JournalEntries.Where(log => log.LogText.Value.Contains("accused of a crime")));
                Assert.Equal(-50, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
            });
            foreach (var instance in Instances)
                instance.Call(() => Assert.Equal(90f, Get<Hero>(instance, scenario.NotableId).Power));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void SeparateGivers_AllowOneCopyPerPlayerAndKeepOtherPlayersQuestUnchanged(bool samePlayer, bool crime)
    {
        Accept();
        var secondGiverId = environment.CreateRegisteredObject<Hero>();
        const string secondIssueId = "grain_second_issue";
        AddIssue(secondGiverId, secondIssueId);

        var acceptingClient = samePlayer ? ClientA : ClientB;
        Quest secondQuest = null!;
        acceptingClient.Call(() =>
        {
            var giver = Get<Hero>(acceptingClient, secondGiverId);
            giver.Issue.StartIssueWithQuest();
            if (!samePlayer)
            {
                secondQuest = (Quest)giver.Issue.IssueQuest;
                secondQuest.QuestAcceptedConsequences();
            }
        }, Texts);
        Server.Call(() =>
        {
            var owned = Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(secondGiverId, secondIssueId, out var controller);
            Assert.Equal(!samePlayer, owned);
            if (owned) Assert.Equal(QuestScenario.ControllerB, controller);
        });
        if (samePlayer) return;

        if (crime)
        {
            Server.Call(() =>
            {
                var hero = Get<Hero>(Server, scenario.PlayerA.HeroId);
                using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
                    DeclareWarAction.ApplyInternal(Get<Kingdom>(Server, kingdomId), hero.MapFaction,
                        DeclareWarAction.DeclareWarDetail.CausedByCrimeRatingChange);
            }, Texts);
        }
        else
        {
            StageDelivery(true, true);
            ClientA.Call(() => QuestHere(ClientA).Success(), Texts);
        }
        ClientB.Call(() =>
        {
            Assert.True(secondQuest.IsOngoing);
            Assert.Equal(0, secondQuest._playerAcceptedQuestLog.CurrentProgress);
            Assert.Equal(0, Get<MobileParty>(ClientB, scenario.PlayerB.PartyId).ItemRoster.GetItemNumber(DefaultItems.Grain));
            Assert.Equal(0, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
            Assert.Equal(0, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Generosity));
            Assert.Same(secondQuest, Get<Hero>(ClientB, secondGiverId).Issue.IssueQuest);
        });
    }

    [Fact]
    public void Crime_CancelsTheOtherOwnersQuestWhenBothShareTheResponsiblePlayersKingdom()
    {
        Accept();
        var secondGiverId = environment.CreateRegisteredObject<Hero>();
        var playerKingdomId = environment.CreateRegisteredObject<Kingdom>();
        const string secondIssueId = "grain_second_issue";
        AddIssue(secondGiverId, secondIssueId);
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    foreach (var clanId in new[] { playerClanId, otherPlayerClanId })
                        Get<Clan>(instance, clanId)._kingdom = Get<Kingdom>(instance, playerKingdomId);
                }
            });
        }
        Quest secondQuest = null!;
        ClientB.Call(() =>
        {
            var giver = Get<Hero>(ClientB, secondGiverId);
            giver.Issue.StartIssueWithQuest();
            secondQuest = (Quest)giver.Issue.IssueQuest;
            secondQuest.QuestAcceptedConsequences();
        }, Texts);

        Server.Call(() =>
        {
            var hero = Get<Hero>(Server, scenario.PlayerA.HeroId);
            using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
                DeclareWarAction.ApplyInternal(Get<Kingdom>(Server, kingdomId), hero.MapFaction,
                    DeclareWarAction.DeclareWarDetail.CausedByCrimeRatingChange);
        }, Texts);

        var outcomes = Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>().ToDictionary(m => m.IssueId, m => m.Outcome);
        Assert.Equal(IssueOutcome.QuestFail, outcomes[QuestScenario.IssueId]);
        Assert.Equal(IssueOutcome.QuestCancel, outcomes[secondIssueId]);
        ClientA.Call(() => Assert.Equal(-50, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor)));
        ClientB.Call(() =>
        {
            Assert.False(secondQuest.IsOngoing);
            Assert.Single(secondQuest.JournalEntries.Where(log => log.LogText.Value == secondQuest.CancelLogOnWarDeclared.Value));
            Assert.Equal(0, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
        });
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void CompanionSolution_ChargesTheOwnerAndReturnsTroopsOnTheServerForEitherOutcome(bool failure, bool poor, bool busy)
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        var funding = 0;
        var villageGold = 0;
        Server.Call(() => villageGold = Get<Village>(Server, villageId).Gold);
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var companion = Get<Hero>(instance, companionId);
                    companion.Clan = Get<Clan>(instance, playerClanId);
                    companion.HeroDeveloper.SetInitialSkillLevel(DefaultSkills.Trade, 120);
                    var troop = Get<CharacterObject>(instance, troopId);
                    troop.Level = 11;
                    troop.UpgradeTargets = Array.Empty<CharacterObject>();
                    var issue = IssueHere(instance);
                    issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                    issue.AlternativeSolutionSentTroops.AddToCounts(troop, 6);
                }
            });
        }

        if (poor)
        {
            Server.Call(() =>
            {
                using (new AllowedThread()) Get<Hero>(Server, scenario.PlayerA.HeroId).Gold = 0;
            });
        }
        ClientA.Call(() =>
        {
            var issue = IssueHere(ClientA);
            funding = issue.AlternativeSolutionNeededGold;
            issue.AlternativeSolutionStartConsequence();
            issue.StartIssueWithAlternativeSolution();
        }, Texts);
        environment.FlushCoalescer();
        if (poor)
        {
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAcceptRejected>());
            Server.Call(() =>
            {
                Assert.True(IssueHere(Server).IsOngoingWithoutQuest);
                Assert.Equal(0, Get<Hero>(Server, scenario.PlayerA.HeroId).Gold);
            });
            return;
        }
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
        Server.Call(() => Assert.Equal(10000 - funding, Get<Hero>(Server, scenario.PlayerA.HeroId).Gold));
        foreach (var instance in Instances)
            instance.Call(() => Assert.Equal(villageGold + funding, Get<Village>(instance, villageId).Gold));

        var ownerIssue = (Issue)GetIssue(ClientA);
        foreach (var instance in new[] { Server, ClientA })
            instance.Call(() => IssueHere(instance).AlternativeSolutionIssueEffectClearTime = CampaignTime.Now);
        var progress = 0;
        Server.Call(() =>
        {
            Campaign.Current.IssueManager.DailyTick();
            progress = IssueHere(Server).JournalEntries[0].CurrentProgress;
            Assert.True(progress > 0);
        }, Texts);
        // The owner's client runs its own daily tick on the same day
        ClientA.Call(() =>
        {
            Campaign.Current.IssueManager.DailyTick();
            Assert.Equal(progress, ownerIssue.JournalEntries[0].CurrentProgress);
            Assert.Single(ownerIssue.JournalEntries.Where(log => log.LogText.Value == ownerIssue.AlternativeSolutionEndLogDefault.Value));
        }, Texts);
        // The framework gives non-owners a placeholder entry so their daily tick has a progress log.
        ClientB.Call(() => Assert.Single(IssueHere(ClientB).JournalEntries));
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                Assert.Equal(0f, IssueHere(instance).GetActiveIssueEffectAmount(DefaultIssueEffects.SettlementProsperity));
                Assert.Equal(0f, IssueHere(instance).GetActiveIssueEffectAmount(DefaultIssueEffects.SettlementLoyalty));
            });

        Server.Call(() =>
        {
            var issue = IssueHere(Server);
            if (busy)
            {
                var hero = Get<Hero>(Server, scenario.PlayerA.HeroId);
                using (new AllowedThread()) hero.ChangeState(Hero.CharacterStates.Prisoner);
                issue.CompleteIssueWithAlternativeSolution();
                Assert.Same(issue, IssueHere(Server));
                Assert.Equal(7, issue.AlternativeSolutionSentTroops.TotalManCount);
                using (new AllowedThread()) hero.ChangeState(Hero.CharacterStates.Active);
            }
            issue._failureChance = failure ? 1f : -1f;
            issue.CompleteIssueWithAlternativeSolution();
        }, Texts);
        environment.FlushCoalescer();

        Assert.Equal(IssueOutcome.AlternativeSolution, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.Null(Get<Hero>(instance, scenario.NotableId).Issue);
                var party = Get<MobileParty>(instance, scenario.PlayerA.PartyId);
                Assert.Equal(6, party.MemberRoster.GetTroopCount(Get<CharacterObject>(instance, troopId)));
                Assert.True(party.MemberRoster.Contains(Get<Hero>(instance, companionId).CharacterObject));
                Assert.True(Get<Hero>(instance, companionId).HeroState == Hero.CharacterStates.Active,
                    $"{instance.GetType().Name} companion {Get<Hero>(instance, companionId).HeroState}");
                Assert.Equal(10000, Get<Hero>(instance, scenario.PlayerB.HeroId).Gold);
                Assert.Equal(failure ? 990f : 1050f, Get<Town>(instance, townId).Prosperity);
                Assert.Equal(10000 - funding, Get<Hero>(instance, scenario.PlayerA.HeroId).Gold);
                Assert.Equal(failure ? 95f : 110f, Get<Hero>(instance, scenario.NotableId).Power);
                Assert.Equal(failure ? 0f : 1f, Get<Clan>(instance, playerClanId).Renown);
                Assert.Equal(failure ? -3 : 5, Get<Hero>(instance, scenario.PlayerA.HeroId).GetRelation(Get<Hero>(instance, scenario.NotableId)));
                Assert.Equal(failure ? -3 : 1, Get<Hero>(instance, scenario.PlayerA.HeroId).GetRelation(Get<Hero>(instance, otherNotableId)));
            });
        }
        ClientA.Call(() =>
        {
            Assert.Equal(failure ? 0 : 30, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Generosity));
            if (failure)
                Assert.Single(ownerIssue.JournalEntries.Where(log => log.LogText.Value.Contains("has failed to resolve the matter")));
            else
                Assert.Single(ownerIssue.JournalEntries.Where(log => log.LogText.Value == ownerIssue.IssueAlternativeSolutionSuccessLog.Value));
        });
    }

    private IssueBase GetIssue(EnvironmentInstance instance)
    {
        IssueBase issue = null!;
        instance.Call(() => issue = IssueHere(instance));
        return issue;
    }

    private void Accept(bool start = true)
    {
        ClientA.Call(() =>
        {
            IssueHere(ClientA).StartIssueWithQuest();
            if (start) QuestHere(ClientA).QuestAcceptedConsequences();
        }, Texts);
    }

    private void AddIssue(string giverId, string issueId)
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var giver = Get<Hero>(instance, giverId);
                    ConfigureGiver(instance, giver);
                    var issue = new Issue(giver)
                    {
                        StringId = issueId,
                        _issueState = IssueBase.IssueState.Ongoing,
                        _nearbySuitableSettlementCache = Get<Settlement>(instance, townSettlementId),
                    };
                    Campaign.Current.IssueManager._issues.Add(giver, issue);
                    giver.OnIssueCreatedForHero(issue);
                }
            });
        }
    }

    private void StageDelivery(bool atVillage, bool enoughGrain)
    {
        Server.Call(() =>
        {
            var party = Get<MobileParty>(Server, scenario.PlayerA.PartyId);
            using (new AllowedThread()) party.CurrentSettlement = atVillage ? Get<Settlement>(Server, villageSettlementId) : null;
            party.ItemRoster.Clear();
            party.ItemRoster.AddToCounts(DefaultItems.Grain, QuestHere(Server)._neededGrainAmount - (enoughGrain ? 0 : 1));
        });
        environment.FlushCoalescer();
    }

    private void ConfigureGiver(EnvironmentInstance instance, Hero notable)
    {
        var settlement = Get<Settlement>(instance, villageSettlementId);
        var village = Get<Village>(instance, villageId);
        var townSettlement = Get<Settlement>(instance, townSettlementId);
        var town = Get<Town>(instance, townId);
        DefaultItems.Grain.ItemCategory = Get<ItemCategory>(instance, grainCategoryId);
        townSettlement.SetSettlementComponent(town);
        settlement.SetSettlementComponent(village);
        village.Bound = townSettlement;
        town.Prosperity = 1000f;
        notable.StayingInSettlement = settlement;
        notable.Occupation = Occupation.Headman;
        var clan = Get<Clan>(instance, giverClanId);
        clan._kingdom = Get<Kingdom>(instance, kingdomId);
        notable.Clan = null;
        notable.AddPower(100f - notable.Power);
        var other = Get<Hero>(instance, otherNotableId);
        other.Occupation = Occupation.Headman;
        other.StayingInSettlement = settlement;
        other.Clan = null;
        settlement._notablesCache ??= new MBList<Hero>();
        if (!settlement._notablesCache.Contains(notable)) settlement._notablesCache.Add(notable);
        if (!settlement._notablesCache.Contains(other)) settlement._notablesCache.Add(other);
        town.OwnerClan = clan;
        Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[]
        {
            new Behavior { _averageGrainPriceInCalradia = 25 },
        }));
    }

    private Issue IssueHere(EnvironmentInstance instance) => (Issue)Get<Hero>(instance, scenario.NotableId).Issue;
    private Quest QuestHere(EnvironmentInstance instance) => Assert.IsType<Quest>(IssueHere(instance).IssueQuest);
    private static T Get<T>(EnvironmentInstance instance, string id)
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var obj));
        return obj;
    }

    private sealed class TestDataStore : IDataStore
    {
        private readonly Dictionary<string, object> records;
        public bool IsSaving { get; }
        public bool IsLoading => !IsSaving;

        public TestDataStore(bool isSaving, Dictionary<string, object> records)
        {
            IsSaving = isSaving;
            this.records = records;
        }

        public bool SyncData<T>(string key, ref T data)
        {
            if (IsSaving)
            {
                records[key] = data!;
                return true;
            }
            if (!records.TryGetValue(key, out var value)) return false;
            data = (T)value;
            return true;
        }
    }
}
