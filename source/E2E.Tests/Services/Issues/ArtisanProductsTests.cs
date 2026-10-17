using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ArtisanProducts;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

namespace E2E.Tests.Services.Issues;

public class ArtisanProductsTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly string originId;
    private readonly string destinationId;
    private readonly string merchantId;
    private readonly string targetId;
    private readonly string clanId;
    private readonly string townId;
    private readonly Harmony encyclopedia = new("artisan-products-test-encyclopedia");

    private EnvironmentInstance Server => scenario.Server;
    private EnvironmentInstance Owner => scenario.ClientA;
    private EnvironmentInstance Other => scenario.ClientB;

    public ArtisanProductsTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        // The headless campaign has no encyclopedia page registry.
        encyclopedia.Patch(AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.EncyclopediaLink)),
            prefix: new HarmonyMethod(typeof(ArtisanProductsTests), nameof(EncyclopediaLink)));
        encyclopedia.Patch(AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.EncyclopediaLink)),
            prefix: new HarmonyMethod(typeof(ArtisanProductsTests), nameof(EncyclopediaLink)));
        originId = environment.CreateRegisteredObject<Settlement>();
        destinationId = environment.CreateRegisteredObject<Settlement>();
        merchantId = environment.CreateRegisteredObject<Hero>();
        targetId = environment.CreateRegisteredObject<Hero>();
        clanId = environment.CreateRegisteredObject<Clan>();
        townId = environment.CreateRegisteredObject<Town>();
        scenario = new QuestScenario(environment, SetupIssue);
        environment.FlushCoalescer();
        foreach (var instance in new[] { Server, Owner, Other })
            instance.Call(() =>
            {
                using (new AllowedThread()) OwnerParty(instance).ItemRoster.Clear();
            });
    }

    public void Dispose()
    {
        environment.Dispose();
        encyclopedia.UnpatchAll(encyclopedia.Id);
    }

    private static bool EncyclopediaLink(ref string __result)
    {
        __result = "test-hero";
        return false;
    }

    private void SetupIssue(EnvironmentInstance instance, string notableId)
    {
        using (new AllowedThread())
        {
            var giver = instance.GetRegisteredObject<Hero>(notableId);
            var origin = instance.GetRegisteredObject<Settlement>(originId);
            var destination = instance.GetRegisteredObject<Settlement>(destinationId);
            var merchant = instance.GetRegisteredObject<Hero>(merchantId);
            var target = instance.GetRegisteredObject<Hero>(targetId);
            var town = instance.GetRegisteredObject<Town>(townId);
            var clan = instance.GetRegisteredObject<Clan>(clanId);
            origin.SetSettlementComponent(town);
            town._ownerClan = clan;
            giver.StayingInSettlement = origin;
            giver.Occupation = Occupation.Artisan;
            giver.Clan = clan;
            merchant.StayingInSettlement = origin;
            merchant.Occupation = Occupation.Merchant;
            merchant.ChangeState(Hero.CharacterStates.Active);
            target.StayingInSettlement = destination;
            target.ChangeState(Hero.CharacterStates.Active);
            origin.AddHeroWithoutParty(merchant);

            // Only generation is staged; acceptance, real quest creation and branches use production code.
            var issue = ObjectHelper.SkipConstructor<Issue>();
            issue.IssueOwner = giver;
            issue.StringId = QuestScenario.IssueId;
            issue._issueState = IssueBase.IssueState.Ongoing;
            issue._targetSettlement = destination;
            issue._targetHero = target;
            issue._rawMaterialsToBeDelivered = DefaultItems.Grain;
            issue.CounterOfferHero = merchant;
            AccessTools.Field(typeof(IssueBase), "_journalEntries").SetValue(issue, new MBList<JournalLog>());
            AccessTools.Field(typeof(IssueBase), "_trackedObjects").SetValue(issue, new List<ITrackableCampaignObject>());
            AccessTools.Field(typeof(IssueBase), nameof(IssueBase.AlternativeSolutionSentTroops)).SetValue(issue, TroopRoster.CreateDummyTroopRoster());
            Campaign.Current.IssueManager._issues.Add(giver, issue);
            giver.OnIssueCreatedForHero(issue);

            if (Campaign.Current.IssueManager.Issues.Count > 1) return;
            foreach (var controllerId in new[] { QuestScenario.ControllerA, QuestScenario.ControllerB })
            {
                // QuestScenario registers players on the server before constructing the issues.
                var players = environment.Server.Resolve<IPlayerManager>();
                Assert.True(players.TryGetPlayer(controllerId, out var player));
                var hero = instance.GetRegisteredObject<Hero>(player.HeroId);
                var party = instance.GetRegisteredObject<MobileParty>(player.MobilePartyId);
                hero.PartyBelongedTo = party;
                party.CurrentSettlement = origin;
                party.ItemRoster.Clear();
                if (controllerId == QuestScenario.ControllerA)
                {
                    hero.Clan = clan;
                    clan._leader = hero;
                    clan.Influence = 1000f;
                }
                if ((instance == environment.Clients.First() && controllerId == QuestScenario.ControllerA) ||
                    (instance == environment.Clients.Last() && controllerId == QuestScenario.ControllerB))
                {
                    Game.Current.PlayerTroop = hero.CharacterObject;
                    Campaign.Current.MainParty = party;
                }
            }
        }
    }

    private void Accept()
    {
        scenario.Accept(Owner);
        environment.FlushCoalescer();
        Owner.Call(() =>
        {
            var quest = (Quest)scenario.QuestOf(Owner);
            quest.QuestAcceptedConsequences();
        });
    }

    private MobileParty OwnerParty(EnvironmentInstance instance)
    {
        Assert.True(environment.Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
        return instance.GetRegisteredObject<MobileParty>(player.MobilePartyId);
    }

    private void CarryAtDestination(int count)
    {
        foreach (var instance in new[] { Server, Owner, Other })
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var party = OwnerParty(instance);
                    party.CurrentSettlement = instance.GetRegisteredObject<Settlement>(destinationId);
                    party.ItemRoster.Clear();
                    party.ItemRoster.AddToCounts(DefaultItems.Grain, count);
                }
            });
    }

    [Fact]
    public void Acceptance_CreatesOnlyTheOwnersJournalAndGrantsGoodsOnceOnTheServer()
    {
        Accept();
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        Server.Call(() =>
        {
            var quest = (Quest)scenario.QuestOf(Server);
            Assert.Equal(quest._amountOfRawGoodsToBeDelivered, OwnerParty(Server).ItemRoster.GetItemNumber(DefaultItems.Grain));
        });
        Other.Call(() => Assert.Null(Other.GetRegisteredObject<Hero>(scenario.NotableId).Issue.IssueQuest));
        Owner.Call(() =>
        {
            var quest = (Quest)scenario.QuestOf(Owner);
            Assert.Single(quest.JournalEntries);
            Assert.Equal(quest._amountOfRawGoodsToBeDelivered, OwnerParty(Owner).ItemRoster.GetItemNumber(DefaultItems.Grain));
        });
    }

    [Fact]
    public void Creation_UsesTheCapturedTargetsWithoutRequiringALocalTargetRoll()
    {
        var itemId = environment.CreateRegisteredObject<ItemObject>();
        byte[] captured = null!;
        Server.Call(() =>
        {
            var issue = (Issue)Server.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
            issue._rawMaterialsToBeDelivered = Server.GetRegisteredObject<ItemObject>(itemId);
            Assert.True(Server.Resolve<IQuestTypeRegistry>().TryGet(typeof(Issue), out var descriptor));
            captured = descriptor.CreationCaptureStrategy.Capture(issue);
        });
        Owner.Call(() =>
        {
            Assert.True(Owner.Resolve<IQuestTypeRegistry>().TryGet(typeof(Issue), out var descriptor));
            var issue = (Issue)descriptor.CreationCaptureStrategy.CreateIssue(Owner.GetRegisteredObject<Hero>(scenario.NotableId), captured);
            Assert.Same(Owner.GetRegisteredObject<Hero>(targetId), issue._targetHero);
            Assert.Same(Owner.GetRegisteredObject<Settlement>(destinationId), issue._targetSettlement);
            Assert.Same(Owner.GetRegisteredObject<Hero>(merchantId), issue.CounterOfferHero);
            Assert.Equal(captured, descriptor.CreationCaptureStrategy.Capture(issue));
        });
    }

    [Fact]
    public void PartialDelivery_UpdatesOnlyTheOwnerAndConsumesOnlyTheirCarriedGoods()
    {
        Accept();
        CarryAtDestination(3);
        Owner.Call(() => ((Quest)scenario.QuestOf(Owner)).DeliverItemsPartiallyOnConsequence());
        Server.Call(() =>
        {
            Assert.Equal(3, ((Quest)scenario.QuestOf(Server))._deliveredRawGoods);
            Assert.Equal(0, OwnerParty(Server).ItemRoster.GetItemNumber(DefaultItems.Grain));
        });
        Owner.Call(() => Assert.Equal(3, ((Quest)scenario.QuestOf(Owner))._playerStartsQuestLog.CurrentProgress));
        Other.Call(() => Assert.Empty(Campaign.Current.QuestManager.Quests));
    }

    [Fact]
    public void FullDelivery_WithMissingGoodsDoesNotFinishOrPay()
    {
        Accept();
        CarryAtDestination(0);
        Owner.Call(() => ((Quest)scenario.QuestOf(Owner)).DeliverItemsFullyOnConsequence());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Server.Call(() => Assert.Equal(0, ((Quest)scenario.QuestOf(Server))._deliveredRawGoods));
    }

    [Fact]
    public void FullDelivery_FinishesEverywhereAndPaysOnlyTheOwner()
    {
        Accept();
        var amount = ((Quest)scenario.QuestOf(Server))._amountOfRawGoodsToBeDelivered;
        var reward = ((Quest)scenario.QuestOf(Server))._rewardGold;
        var before = OwnerGold();
        CarryAtDestination(amount);
        Owner.Call(() => ((Quest)scenario.QuestOf(Owner)).DeliverItemsFullyOnConsequence());
        Assert.Equal(IssueOutcome.QuestSuccess, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Equal(before + reward, OwnerGold());
        Owner.Call(() => Assert.Null(Owner.GetRegisteredObject<Hero>(scenario.NotableId).Issue));
        Other.Call(() => Assert.Empty(Campaign.Current.QuestManager.Quests));
    }

    private int OwnerGold()
    {
        var gold = 0;
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
            gold = Server.GetRegisteredObject<Hero>(player.HeroId).Gold;
        });
        return gold;
    }

    [Fact]
    public void ARealStayAliveRefusal_RejectsAcceptance()
    {
        Server.Call(() => Server.GetRegisteredObject<Hero>(targetId).ChangeState(Hero.CharacterStates.Dead));
        scenario.Accept(Owner);
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
    }

    [Fact]
    public void TwoPlayers_CanAcceptTheirOwnCopyOfTheRealQuest()
    {
        Accept();
        var second = environment.CreateRegisteredObject<Hero>();
        foreach (var instance in new[] { Server, Owner, Other }) instance.Call(() => SetupIssue(instance, second));
        Other.Call(() => Other.GetRegisteredObject<Hero>(second).Issue.StartIssueWithQuest());
        environment.FlushCoalescer();
        Other.Call(() => ((Quest)Other.GetRegisteredObject<Hero>(second).Issue.IssueQuest).QuestAcceptedConsequences());
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(second, QuestScenario.IssueId, out var controller));
            Assert.Equal(QuestScenario.ControllerB, controller);
            Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        });
        Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>().Count());
        Owner.Call(() => Assert.Single(Campaign.Current.QuestManager.Quests));
        Other.Call(() => Assert.Single(Campaign.Current.QuestManager.Quests));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LordChoice_UsesTheServerAndKeepsTheJournalOnTheOwner(bool acceptMerchant)
    {
        Owner.Call(() => Owner.GetRegisteredObject<Hero>(scenario.NotableId).Issue.StartIssueWithLordSolution());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        Other.Call(() => Assert.Empty(Other.GetRegisteredObject<Hero>(scenario.NotableId).Issue.JournalEntries));
        var before = OwnerGold();
        LeaveOrigin();
        Owner.Call(() =>
        {
            var issue = Owner.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
            if (acceptMerchant) issue.CompleteIssueWithLordSolutionWithAcceptCounterOffer();
            else issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer();
        });
        Assert.Equal(acceptMerchant, before == OwnerGold());
        foreach (var instance in new[] { Server, Owner, Other })
            instance.Call(() => Assert.Null(instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue));
    }

    [Fact]
    public void MerchantRefusal_AppliesOnceAndAcknowledgesTheOwnersDialogueState()
    {
        Accept();
        LeaveOrigin();
        Owner.Call(() => ((Quest)scenario.QuestOf(Owner)).RefuseCounterOfferConsequences());
        Owner.Call(() => ((Quest)scenario.QuestOf(Owner)).RefuseCounterOfferConsequences());
        Owner.Call(() => Assert.True(((Quest)scenario.QuestOf(Owner))._counterOfferRefused));
        Server.Call(() => Assert.True(((Quest)scenario.QuestOf(Server))._counterOfferRefused));
    }

    [Fact]
    public void MerchantSurrender_AfterLeavingTownRemovesTheGoodsAndFailsTheQuest()
    {
        Accept();
        LeaveOrigin();
        Owner.Call(() => ((Quest)scenario.QuestOf(Owner)).QuestFailedWithRefusal());
        Assert.Equal(IssueOutcome.QuestFail, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Server.Call(() => Assert.Equal(0, OwnerParty(Server).ItemRoster.GetItemNumber(DefaultItems.Grain)));
        Owner.Call(() => Assert.Null(Owner.GetRegisteredObject<Hero>(scenario.NotableId).Issue));
    }

    private void LeaveOrigin()
    {
        foreach (var instance in new[] { Server, Owner, Other })
            instance.Call(() =>
            {
                using (new AllowedThread()) OwnerParty(instance).CurrentSettlement = null;
            });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompanionCompletion_ReturnsTroopsAndKeepsThePaymentWithTheOwner(bool fail)
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        foreach (var instance in new[] { Server, Owner, Other })
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var issue = instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
                    issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                    issue._issueDifficultyMultiplier = 1f;
                    issue._failureChance = fail ? 2f : -1f;
                    issue._companionRewardSkill = DefaultSkills.Trade;
                    instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(scenario.NotableId, issue.StringId, QuestScenario.ControllerA);
                    if (instance != Other)
                    {
                        issue.AlternativeSolutionSentTroops.AddToCounts(instance.GetRegisteredObject<Hero>(companionId).CharacterObject, 1);
                        issue.AlternativeSolutionSentTroops.AddToCounts(instance.GetRegisteredObject<CharacterObject>(troopId), 9);
                    }
                }
            });
        var before = OwnerGold();
        Server.Call(() => Server.GetRegisteredObject<Hero>(scenario.NotableId).Issue.CompleteIssueWithAlternativeSolution());
        Assert.Equal(IssueOutcome.AlternativeSolution, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Equal(fail ? before : before + 2000, OwnerGold());
        Server.Call(() =>
        {
            var party = OwnerParty(Server);
            Assert.Equal(1, party.MemberRoster.GetTroopCount(Server.GetRegisteredObject<Hero>(companionId).CharacterObject));
            Assert.Equal(9, party.MemberRoster.GetTroopCount(Server.GetRegisteredObject<CharacterObject>(troopId)));
        });
    }

    [Fact]
    public void TargetDeath_CancelsOnTheServerAndRemovesTheOwnersRealQuest()
    {
        Accept();
        Server.Call(() => CampaignEventDispatcher.Instance.OnHeroKilled(
            Server.GetRegisteredObject<Hero>(targetId), null,
            TaleWorlds.CampaignSystem.Actions.KillCharacterAction.KillCharacterActionDetail.DiedInBattle, false));
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Owner.Call(() => Assert.Null(Owner.GetRegisteredObject<Hero>(scenario.NotableId).Issue));
        Other.Call(() => Assert.Null(Other.GetRegisteredObject<Hero>(scenario.NotableId).Issue));
    }

    [Theory]
    [InlineData(IssueBase.IssueState.SolvingWithAlternativeSolution)]
    [InlineData(IssueBase.IssueState.SolvingWithLordSolution)]
    public void GiverDeath_EndsQuestlessSolutionsOnEveryPeer(IssueBase.IssueState state)
    {
        var troopId = state == IssueBase.IssueState.SolvingWithAlternativeSolution ? environment.CreateRegisteredObject<CharacterObject>() : null;
        foreach (var instance in new[] { Server, Owner, Other })
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var issue = instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
                    issue._issueState = state;
                    issue.IsTriedToSolveBefore = instance == Owner;
                    instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(scenario.NotableId, issue.StringId, QuestScenario.ControllerA);
                    if (troopId != null && instance != Other)
                        issue.AlternativeSolutionSentTroops.AddToCounts(instance.GetRegisteredObject<CharacterObject>(troopId), 9);
                }
            });
        Server.Call(() => CampaignEventDispatcher.Instance.OnHeroKilled(Server.GetRegisteredObject<Hero>(scenario.NotableId), null,
            KillCharacterAction.KillCharacterActionDetail.DiedInBattle, false));
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        environment.FlushCoalescer();
        foreach (var instance in new[] { Server, Owner, Other })
            instance.Call(() => Assert.Null(instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue));
        if (troopId != null)
            foreach (var instance in new[] { Server, Owner })
                instance.Call(() => Assert.Equal(9, OwnerParty(instance).MemberRoster.GetTroopCount(instance.GetRegisteredObject<CharacterObject>(troopId))));
    }

    [Theory]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByRebellion, IssueOutcome.QuestCancel)]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility, IssueOutcome.QuestFail)]
    public void War_CancelsOrFailsAsTheOwningPlayer(DeclareWarAction.DeclareWarDetail detail, IssueOutcome outcome)
    {
        Accept();
        var playerClanId = environment.CreateRegisteredObject<Clan>();
        foreach (var instance in new[] { Server, Owner, Other })
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Assert.True(environment.Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var player));
                    instance.GetRegisteredObject<Hero>(player.HeroId).Clan = instance.GetRegisteredObject<Clan>(playerClanId);
                }
            });
        Server.Call(() => DeclareWarAction.ApplyInternal(Server.GetRegisteredObject<Clan>(clanId).MapFaction,
            Server.GetRegisteredObject<Clan>(playerClanId).MapFaction, detail));
        Assert.Equal(outcome, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Owner.Call(() => Assert.Null(Owner.GetRegisteredObject<Hero>(scenario.NotableId).Issue));
    }

    [Fact]
    public void Inheritance_MovesTheFrameworkOwnerOnTheGameThread()
    {
        Accept();
        var heirId = environment.CreateRegisteredObject<Hero>();
        Server.Call(() => Campaign.Current.IssueManager.ChangeIssueOwner(
            Server.GetRegisteredObject<Hero>(scenario.NotableId).Issue, Server.GetRegisteredObject<Hero>(heirId)));
        foreach (var instance in new[] { Server, Owner, Other })
            instance.Call(() =>
            {
                Assert.Null(instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue);
                var issue = instance.GetRegisteredObject<Hero>(heirId).Issue;
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwner(heirId, issue.StringId, out var controller));
                Assert.Equal(QuestScenario.ControllerA, controller);
            });
    }

    [Fact]
    public void DeliveryAfterInheritance_UsesTheHeirsSettlementWhenTheOldGiverHasLeft()
    {
        Accept();
        var quest = (Quest)scenario.QuestOf(Owner);
        var heirId = environment.CreateRegisteredObject<Hero>();
        foreach (var instance in new[] { Server, Owner, Other })
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    instance.GetRegisteredObject<Hero>(heirId).StayingInSettlement = instance.GetRegisteredObject<Settlement>(originId);
                    instance.GetRegisteredObject<Hero>(heirId).Occupation = Occupation.Artisan;
                    instance.GetRegisteredObject<Hero>(heirId).Clan = instance.GetRegisteredObject<Clan>(clanId);
                }
            });
        Server.Call(() => Campaign.Current.IssueManager.ChangeIssueOwner(
            Server.GetRegisteredObject<Hero>(scenario.NotableId).Issue, Server.GetRegisteredObject<Hero>(heirId)));
        foreach (var instance in new[] { Server, Owner, Other })
            instance.Call(() =>
            {
                using (new AllowedThread()) instance.GetRegisteredObject<Hero>(scenario.NotableId).StayingInSettlement = null;
            });
        CarryAtDestination(quest._amountOfRawGoodsToBeDelivered);
        var before = 0f;
        Server.Call(() => before = Server.GetRegisteredObject<Town>(townId).Prosperity);
        Owner.Call(quest.DeliverItemsFullyOnConsequence);
        Assert.Equal(IssueOutcome.QuestSuccess, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Server.Call(() => Assert.Equal(before + 30f, Server.GetRegisteredObject<Town>(townId).Prosperity));
    }
}
