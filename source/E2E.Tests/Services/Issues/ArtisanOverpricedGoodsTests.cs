using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ArtisanOverpricedGoods;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

namespace E2E.Tests.Services.Issues;

public class ArtisanOverpricedGoodsTests : IDisposable
{
    private const int Amount = 5;
    private const int Payment = 900;
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly string merchantId;
    private readonly string itemId;
    private readonly string settlementId;
    private readonly string townId;
    private readonly string clanId;
    private readonly string otherClanId;

    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance ClientA => environment.Clients.First();
    private EnvironmentInstance ClientB => environment.Clients.Last();
    private IEnumerable<EnvironmentInstance> Instances => environment.Clients.Prepend(Server);

    public ArtisanOverpricedGoodsTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        merchantId = environment.CreateRegisteredObject<Hero>();
        itemId = environment.CreateRegisteredObject<ItemObject>();
        settlementId = environment.CreateRegisteredObject<Settlement>();
        townId = environment.CreateRegisteredObject<Town>();
        clanId = environment.CreateRegisteredObject<Clan>();
        otherClanId = environment.CreateRegisteredObject<Clan>();
        var categoryId = environment.CreateRegisteredObject<ItemCategory>();

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var town = Get<Town>(instance, townId);
                    var settlement = Get<Settlement>(instance, settlementId);
                    var merchant = Get<Hero>(instance, merchantId);
                    var item = Get<ItemObject>(instance, itemId);
                    AccessTools.Field(typeof(Town), nameof(Town._marketData)).SetValue(town, new TownMarketData(town));
                    settlement.SetSettlementComponent(town);
                    Campaign.Current.CampaignObjectManager.Settlements = new MBList<Settlement>(Campaign.Current.Settlements) { settlement };
                    town.OwnerClan = Get<Clan>(instance, clanId);
                    town.Prosperity = 2500;
                    merchant.Occupation = Occupation.Merchant;
                    merchant.Clan = null;
                    merchant.StayingInSettlement = settlement;
                    merchant.HeroState = Hero.CharacterStates.Active;
                    merchant._power = 100;
                    settlement.AddHeroWithoutParty(merchant);
                    item.Value = 80;
                    item.StringId = itemId;
                    item.ItemCategory = Get<ItemCategory>(instance, categoryId);
                    MBObjectManager.Instance.RegisterObject(item);
                    Campaign.Current.Models.TradeItemPriceFactorModel = new ArtisanPriceModel();
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    new IssuesCampaignBehavior().RegisterEvents();
                }
            });
        }

        scenario = new QuestScenario(environment, CreateIssue);
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    SetupPlayer(instance, scenario.OwnerHeroId, scenario.OwnerPartyId, clanId);
                    SetupPlayer(instance, scenario.OtherHeroId, scenario.OtherPartyId, otherClanId);
                    if (instance != Server)
                    {
                        var first = instance == ClientA;
                        Game.Current.PlayerTroop = Get<Hero>(instance, first ? scenario.OwnerHeroId : scenario.OtherHeroId).CharacterObject;
                        Campaign.Current.MainParty = Get<MobileParty>(instance, first ? scenario.OwnerPartyId : scenario.OtherPartyId);
                        Campaign.Current.PlayerDefaultFaction = Get<Clan>(instance, first ? clanId : otherClanId);
                    }
                }
            });
        }
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void Acceptance_StartsOnlyTheOwnersQuestAndRetainsAnUnstartedServerCopy()
    {
        Accept();
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        ClientA.Call(() => Assert.True(QuestHere(ClientA).IsOngoing));
        Server.Call(() => Assert.Null(QuestHere(Server)._playerStartsQuestLog));
        ClientB.Call(() => Assert.Null(Get<Hero>(ClientB, scenario.NotableId).Issue.IssueQuest));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Acceptance_RefusesRealStayAliveAndRelationFailures(bool badRelation)
    {
        Server.Call(() =>
        {
            if (!badRelation)
                ((ArtisanPriceModel)Campaign.Current.Models.TradeItemPriceFactorModel).PriceIndex = 1f;
        });
        if (badRelation)
        {
            Server.Call(() => CharacterRelationManager.SetHeroRelation(Get<Hero>(Server, scenario.OwnerHeroId), Get<Hero>(Server, scenario.NotableId), -11));
        }
        Accept();
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        ClientA.Call(() => Assert.Null(Get<Hero>(ClientA, scenario.NotableId).Issue.IssueQuest));
    }

    [Fact]
    public void PartialThenFullDelivery_UpdatesOnlyTheOwnerAndPaysExactlyOnce()
    {
        Accept();
        Stock(2);
        ClientA.Call(() => QuestHere(ClientA).DeliverItemsPartiallyOnConsequence());
        foreach (var instance in new[] { Server, ClientA })
            instance.Call(() => Assert.Equal(2, QuestHere(instance)._givenTradeGoods));
        ClientA.Call(() => Assert.Equal(2, QuestHere(ClientA)._playerStartsQuestLog.CurrentProgress));
        Server.Call(() => Assert.Equal(10000, Get<Hero>(Server, scenario.OwnerHeroId).Gold));

        Stock(3);
        var clientQuest = (Quest)scenario.QuestOf(ClientA);
        ClientA.Call(() => clientQuest.DeliverItemsFullyOnConsequence());
        // Vanilla's next dialogue line calls this after the delivery consequence.
        ClientA.Call(() => clientQuest.CompleteQuestWithSuccess());
        environment.FlushCoalescer();
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        AssertFinished(2530, 10900);
        Server.Call(() => Assert.Equal(0, Get<MobileParty>(Server, scenario.OwnerPartyId).ItemRoster.GetItemNumber(Get<ItemObject>(Server, itemId))));
    }

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 4)]
    public void Delivery_IsRefusedOutsideTheTownOrWithoutEnoughGoods(bool inTown, int goods)
    {
        Accept();
        Stock(goods);
        Server.Call(() =>
        {
            using (new AllowedThread())
                Get<MobileParty>(Server, scenario.OwnerPartyId).CurrentSettlement = inTown ? Get<Settlement>(Server, settlementId) : null;
        });
        ClientA.Call(() => QuestHere(ClientA).DeliverItemsFullyOnConsequence());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() => Assert.Equal(0, QuestHere(Server)._givenTradeGoods));
    }

    [Fact]
    public void AnEarlySuccessRequest_CannotPayBeforeDelivery()
    {
        Accept();
        ClientA.Call(() => QuestHere(ClientA).CompleteQuestWithSuccess());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() => Assert.Equal(10000, Get<Hero>(Server, scenario.OwnerHeroId).Gold));
    }

    [Fact]
    public void MerchantOffer_FailsTheOwnersQuestAndAppliesTheCombinedTownConsequencesOnce()
    {
        Accept();
        ClientA.Call(() => QuestHere(ClientA).AcceptCounterOffer());
        environment.FlushCoalescer();
        Assert.Equal(IssueOutcome.QuestFail, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        AssertFinished(2450, 10000);
        Server.Call(() => Assert.Equal(105, Get<Hero>(Server, merchantId).Power));
    }

    [Theory]
    [InlineData(false, 2450, IssueOutcome.QuestTimeOut)]
    [InlineData(true, 2500, IssueOutcome.QuestCancel)]
    public void TimeoutAndCancellation_EndEveryCopyWithoutPaying(bool cancel, int prosperity, IssueOutcome outcome)
    {
        Accept();
        ClientA.Call(() =>
        {
            if (cancel) QuestHere(ClientA).CompleteQuestWithCancel();
            else QuestHere(ClientA).CompleteQuestWithTimeOut();
        });
        environment.FlushCoalescer();
        Assert.Equal(outcome, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        AssertFinished(prosperity, 10000);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LordCounterOffer_ChargesAndRewardsOnlyTheOwner(bool acceptMerchant)
    {
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLordSolutionStarted>());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        var cost = 0;
        Server.Call(() => cost = Get<Hero>(Server, scenario.NotableId).Issue.NeededInfluenceForLordSolution);

        ClientA.Call(() =>
        {
            var issue = Get<Hero>(ClientA, scenario.NotableId).Issue;
            if (acceptMerchant) issue.CompleteIssueWithLordSolutionWithAcceptCounterOffer();
            else issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer();
        });
        environment.FlushCoalescer();
        var outcome = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome;
        Assert.Equal(acceptMerchant ? IssueOutcome.LordAcceptedCounterOffer : IssueOutcome.LordRefusedCounterOffer, outcome);
        AssertFinished(acceptMerchant ? 2470 : 2530, acceptMerchant ? 10000 : 10900);
        Server.Call(() => Assert.Equal(acceptMerchant ? 100 : 100 - cost, Get<Clan>(Server, clanId).Influence));
    }

    [Fact]
    public void ASecondPlayer_CannotCompleteTheFirstPlayersLordSolution()
    {
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        ClientB.Call(() => Get<Hero>(ClientB, scenario.NotableId).Issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompanionSolution_ChargesOnceAndReturnsTroopsOnlyToItsOwner(bool failure)
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        var needed = 0;
        var funding = 0;
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var companion = Get<Hero>(instance, companionId);
                    companion.HeroState = Hero.CharacterStates.Active;
                    companion.SetSkillValue(DefaultSkills.Trade, 150);
                    var troop = Get<CharacterObject>(instance, troopId);
                    troop.Level = 20;
                    troop.UpgradeTargets = Array.Empty<CharacterObject>();
                    var issue = (Issue)Get<Hero>(instance, scenario.NotableId).Issue;
                    needed = issue.GetTotalAlternativeSolutionNeededMenCount();
                    funding = issue.RequiredGoldForAlternativeSolution;
                    var party = Get<MobileParty>(instance, scenario.OwnerPartyId);
                    party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                    party.MemberRoster.AddToCounts(troop, needed + 2);
                }
            });
        }

        // PartyScreenLogic's separate commit has already removed the selected troops on the server.
        Server.Call(() =>
        {
            var party = Get<MobileParty>(Server, scenario.OwnerPartyId);
            party.MemberRoster.AddToCounts(Get<Hero>(Server, companionId).CharacterObject, -1);
            party.MemberRoster.AddToCounts(Get<CharacterObject>(Server, troopId), -needed);
        });
        environment.FlushCoalescer();

        ClientA.Call(() =>
        {
            var issue = Get<Hero>(ClientA, scenario.NotableId).Issue;
            var companion = Get<Hero>(ClientA, companionId);
            var troop = Get<CharacterObject>(ClientA, troopId);
            issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
            issue.AlternativeSolutionSentTroops.AddToCounts(troop, needed);
            issue.AlternativeSolutionStartConsequence();
            issue.StartIssueWithAlternativeSolution();
        });
        environment.FlushCoalescer();
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
        Server.Call(() => Assert.Equal(10000 - funding, Get<Hero>(Server, scenario.OwnerHeroId).Gold));
        var ownerIssue = ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue;

        Server.Call(() =>
        {
            var issue = Get<Hero>(Server, scenario.NotableId).Issue;
            issue._failureChance = failure ? 1 : -1;
            issue.CompleteIssueWithAlternativeSolution();
        });
        environment.FlushCoalescer();
        AssertFinished(failure ? 2500 : 2530, 10000 - funding + (failure ? 0 : Payment));
        Assert.True(ownerIssue.JournalEntries.Count >= 2);
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var party = Get<MobileParty>(instance, scenario.OwnerPartyId);
                var companion = Get<Hero>(instance, companionId);
                var troop = Get<CharacterObject>(instance, troopId);
                Assert.Equal(needed + 2, party.MemberRoster.GetTroopCount(troop));
                Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
                Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
                Assert.Equal(0, Get<MobileParty>(instance, scenario.OtherPartyId).MemberRoster.GetTroopCount(troop));
            });
        }
    }

    [Fact]
    public void DifferentPlayers_CanHoldSeparateQuestsWhileASecondAcceptForTheSameOwnerIsRefused()
    {
        Accept();
        var secondId = AddAnotherIssue();
        ClientB.Call(() => StartQuest(ClientB, secondId));
        Server.Call(() =>
        {
            var owners = Server.Resolve<IIssueOwnershipRegistry>();
            Assert.True(owners.TryGetOwner(secondId, QuestScenario.IssueId + secondId, out var owner));
            Assert.Equal(QuestScenario.ControllerB, owner);
        });
        var thirdId = AddAnotherIssue();
        ClientA.Call(() => StartQuest(ClientA, thirdId));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        ClientB.Call(() => Assert.True(((Quest)Get<Hero>(ClientB, secondId).Issue.IssueQuest).IsOngoing));
    }

    private Issue CreateIssue(EnvironmentInstance instance, Hero notable)
    {
        notable.StayingInSettlement = Get<Settlement>(instance, settlementId);
        notable.Occupation = Occupation.Artisan;
        notable.Clan = null;
        notable._power = 200;
        return new Issue(notable, Get<Hero>(instance, merchantId), Get<ItemObject>(instance, itemId))
        {
            RequestedTradeGoodAmount = Amount,
            _goldReward = Payment,
        };
    }

    private void SetupPlayer(EnvironmentInstance instance, string heroId, string partyId, string playerClanId)
    {
        var hero = Get<Hero>(instance, heroId);
        var party = Get<MobileParty>(instance, partyId);
        hero._partyBelongedTo = party;
        hero.Clan = Get<Clan>(instance, playerClanId);
        hero.Clan.SetLeader(hero);
        hero.Gold = 10000;
        party.CurrentSettlement = Get<Settlement>(instance, settlementId);
        Get<Clan>(instance, clanId).Influence = 100;
    }

    private void Accept() => ClientA.Call(() => StartQuest(ClientA, scenario.NotableId));

    private static void StartQuest(EnvironmentInstance instance, string giverId)
    {
        var issue = Get<Hero>(instance, giverId).Issue;
        issue.StartIssueWithQuest();
        if (issue.IssueQuest is Quest quest) quest.QuestAcceptedConsequences();
    }

    private Quest QuestHere(EnvironmentInstance instance) => Assert.IsType<Quest>(Get<Hero>(instance, scenario.NotableId).Issue.IssueQuest);

    private void Stock(int amount)
    {
        Server.Call(() => Get<MobileParty>(Server, scenario.OwnerPartyId).ItemRoster.AddToCounts(Get<ItemObject>(Server, itemId), amount));
        environment.FlushCoalescer();
    }

    private void AssertFinished(float prosperity, int ownerGold)
    {
        var townMessages = string.Join(", ", Server.NetworkSentMessages.Where(message => message.GetType().Name.Contains("Town")).Select(message => message.GetType().Name));
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.Null(Get<Hero>(instance, scenario.NotableId).Issue);
                Assert.True(prosperity == Get<Hero>(instance, scenario.NotableId).CurrentSettlement.Town.Prosperity,
                    $"{instance.GetType().Name} prosperity {Get<Hero>(instance, scenario.NotableId).CurrentSettlement.Town.Prosperity}, expected {prosperity}. Town messages: {townMessages}");
                Assert.Equal(ownerGold, Get<Hero>(instance, scenario.OwnerHeroId).Gold);
                Assert.Equal(10000, Get<Hero>(instance, scenario.OtherHeroId).Gold);
                Assert.False(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _));
            });
        }
    }

    private string AddAnotherIssue()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var giver = Get<Hero>(instance, giverId);
                    var issue = CreateIssue(instance, giver);
                    issue.StringId = QuestScenario.IssueId + giverId;
                    issue._issueState = IssueBase.IssueState.Ongoing;
                    Campaign.Current.IssueManager._issues.Add(giver, issue);
                    giver.OnIssueCreatedForHero(issue);
                }
            });
        }
        return giverId;
    }

    private static T Get<T>(EnvironmentInstance instance, string id) where T : class => instance.GetRegisteredObject<T>(id);
}
