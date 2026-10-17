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
using GameInterface.Services.ItemRosters.Patches;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
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
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.PlayerAcceptedQuestLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.PlayerHasNeededGrainsLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.SuccessLog)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestTimeoutLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.CancelLogOnWarDeclared)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.CancelLogOnVillageRaided)),
        AccessTools.Method(typeof(LogEntry), nameof(LogEntry.AddLogEntry), new[] { typeof(LogEntry) }),
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.SetCharacterProperties)),
        AccessTools.PropertyGetter(typeof(Issue), nameof(Issue.AlternativeSolutionStartLog)),
        AccessTools.PropertyGetter(typeof(Issue), nameof(Issue.IssueAlternativeSolutionSuccessLog)),
    };

    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly string villageSettlementId;
    private readonly string villageId;
    private readonly string townSettlementId;
    private readonly string townId;
    private readonly string kingdomId;
    private readonly string giverClanId;
    private readonly string playerClanId;
    private readonly string grainCategoryId;
    private EnvironmentInstance Server => scenario.Server;
    private EnvironmentInstance ClientA => scenario.ClientA;
    private EnvironmentInstance ClientB => scenario.ClientB;
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public HeadmanNeedsGrainTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        villageSettlementId = environment.CreateRegisteredObject<Settlement>();
        villageId = environment.CreateRegisteredObject<Village>();
        townSettlementId = environment.CreateRegisteredObject<Settlement>();
        townId = environment.CreateRegisteredObject<Town>();
        kingdomId = environment.CreateRegisteredObject<Kingdom>();
        giverClanId = environment.CreateRegisteredObject<Clan>();
        playerClanId = environment.CreateRegisteredObject<Clan>();
        grainCategoryId = environment.CreateRegisteredObject<ItemCategory>();
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
                        hero.Clan = Get<Clan>(instance, playerClanId);
                        hero.Gold = 10000;
                    }

                    if (instance != Server)
                    {
                        var player = instance == ClientA ? scenario.PlayerA : scenario.PlayerB;
                        Game.Current.PlayerTroop = Get<Hero>(instance, player.HeroId).CharacterObject;
                        Campaign.Current.MainParty = Get<MobileParty>(instance, player.PartyId);
                        Campaign.Current.PlayerDefaultFaction = Get<Clan>(instance, playerClanId);
                    }
                }
            });
        }
    }

    public void Dispose() => environment.Dispose();

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
                Assert.Equal(1050f, Get<Town>(instance, townId).Prosperity);
                Assert.Equal(10000, Get<Hero>(instance, scenario.PlayerB.HeroId).Gold);
            });
        }
    }

    [Fact]
    public void Timeout_AppliesTheWorldPenaltyOnceAndEndsEveryCopy()
    {
        Accept();
        ClientA.Call(() => QuestHere(ClientA).CompleteQuestWithTimeOut(), Texts);
        environment.FlushCoalescer();
        Assert.Equal(IssueOutcome.QuestTimeOut, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.Null(Get<Hero>(instance, scenario.NotableId).Issue);
                Assert.Equal(990f, Get<Town>(instance, townId).Prosperity);
            });
        }
    }

    [Fact]
    public void ServerInventoryUpdates_RefreshTheOwnersJournalAndRemoveTheReadyLogWhenGrainIsLost()
    {
        Accept();
        ClientA.Call(() =>
        {
            var quest = QuestHere(ClientA);
            var roster = Get<MobileParty>(ClientA, scenario.PlayerA.PartyId).ItemRoster;
            ItemRosterPatch.AddToCountsOverride(roster, new EquipmentElement(DefaultItems.Grain), quest._neededGrainAmount);
            Assert.Equal(quest._neededGrainAmount, quest._playerAcceptedQuestLog.CurrentProgress);
            Assert.NotNull(quest._playerHasNeededGrainsLog);
            ItemRosterPatch.ClearOverride(roster);
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
        Server.Call(() => DeclareWarAction.ApplyInternal(Get<Kingdom>(Server, kingdomId), Get<Clan>(Server, playerClanId), detail), Texts);
        Assert.Equal(outcome, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeparateGivers_AllowOneCopyPerPlayerAndRefuseTheSamePlayersSecondCopy(bool samePlayer)
    {
        Accept();
        var secondGiverId = environment.CreateRegisteredObject<Hero>();
        const string secondIssueId = "grain_second_issue";
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var giver = Get<Hero>(instance, secondGiverId);
                    ConfigureGiver(instance, giver);
                    var issue = new Issue(giver)
                    {
                        StringId = secondIssueId,
                        _issueState = IssueBase.IssueState.Ongoing,
                        _nearbySuitableSettlementCache = Get<Settlement>(instance, townSettlementId),
                    };
                    Campaign.Current.IssueManager._issues.Add(giver, issue);
                    giver.OnIssueCreatedForHero(issue);
                }
            });
        }

        var acceptingClient = samePlayer ? ClientA : ClientB;
        acceptingClient.Call(() => Get<Hero>(acceptingClient, secondGiverId).Issue.StartIssueWithQuest(), Texts);
        Server.Call(() =>
        {
            var owned = Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(secondGiverId, secondIssueId, out var controller);
            Assert.Equal(!samePlayer, owned);
            if (owned) Assert.Equal(QuestScenario.ControllerB, controller);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompanionSolution_ChargesTheOwnerAndReturnsTroopsOnTheServerForEitherOutcome(bool failure)
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        var funding = 0;
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

        ClientA.Call(() =>
        {
            var issue = IssueHere(ClientA);
            funding = issue.AlternativeSolutionNeededGold;
            issue.AlternativeSolutionStartConsequence();
            issue.StartIssueWithAlternativeSolution();
        }, Texts);
        environment.FlushCoalescer();
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
        Server.Call(() => Assert.Equal(10000 - funding, Get<Hero>(Server, scenario.PlayerA.HeroId).Gold));

        Server.Call(() =>
        {
            var issue = IssueHere(Server);
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
                Assert.Equal(Hero.CharacterStates.Active, Get<Hero>(instance, companionId).HeroState);
                Assert.Equal(10000, Get<Hero>(instance, scenario.PlayerB.HeroId).Gold);
                Assert.Equal(failure ? 990f : 1050f, Get<Town>(instance, townId).Prosperity);
            });
        }
    }

    private void Accept(bool start = true)
    {
        ClientA.Call(() =>
        {
            IssueHere(ClientA).StartIssueWithQuest();
            if (start) QuestHere(ClientA).QuestAcceptedConsequences();
        }, Texts);
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
        notable.Clan = clan;
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
}
