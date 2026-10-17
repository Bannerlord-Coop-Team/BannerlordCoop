using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ArtOfTrade;
using GameInterface.Services.Players;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssueQuest;

namespace E2E.Tests.Services.Issues;

public class ArtOfTradeTests : IDisposable
{
    private static readonly MethodBase[] NativePresentation =
    {
        AccessTools.Method(typeof(Quest), nameof(Quest.SetDialogs)),
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.SetCharacterProperties)),
        AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.EncyclopediaLink)),
        AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation)),
        AccessTools.Method(typeof(Town), nameof(Town.GetItemPrice), new[] { typeof(ItemObject), typeof(MobileParty), typeof(bool) }),
    };

    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly string settlementId;
    private readonly string villageId;
    private readonly string boundSettlementId;
    private readonly string boundTownId;
    private readonly string itemId;
    private readonly string giverClanId;
    private readonly string kingdomId;
    private readonly string playerClanId;
    private EnvironmentInstance Server => scenario.Server;
    private EnvironmentInstance Owner => scenario.ClientA;
    private EnvironmentInstance Other => scenario.ClientB;
    private EnvironmentInstance[] Instances => new[] { Server, Owner, Other };

    public ArtOfTradeTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
        settlementId = environment.CreateRegisteredObject<Settlement>();
        villageId = environment.CreateRegisteredObject<Village>();
        boundSettlementId = environment.CreateRegisteredObject<Settlement>();
        boundTownId = environment.CreateRegisteredObject<Town>();
        itemId = environment.CreateRegisteredObject<ItemObject>();
        giverClanId = environment.CreateRegisteredObject<Clan>();
        kingdomId = environment.CreateRegisteredObject<Kingdom>();
        playerClanId = environment.CreateRegisteredObject<Clan>();

        foreach (var instance in Instances) Configure(instance);
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void CreationCaptureRebuildsTheProductionAndRewardClock()
    {
        byte[] captured = null!;
        Server.Call(() => captured = Descriptor(Server).CreationCaptureStrategy.Capture(IssueOf(Server)));
        Owner.Call(() =>
        {
            using var allowed = new AllowedThread();
            var issue = (Issue)Descriptor(Owner).CreationCaptureStrategy.CreateIssue(Giver(Owner), captured);
            Assert.Same(Item(Owner), issue._selectedItemObject);
            Assert.Equal(IssueOf(Owner).IssueCreationTime, issue.IssueCreationTime);
        });
    }

    [Fact]
    public void AcceptanceUsesTheOwnersQuoteAndLeavesOtherClientsWithoutAQuest()
    {
        Accept();
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        Server.Call(() => Assert.Equal(QuestOf(Owner)._selectedItemObjectCount, QuestOf(Server)._selectedItemObjectCount));
        Other.Call(() => Assert.Null(IssueOf(Other).IssueQuest));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
    }

    [Fact]
    public void CommissionStartsOnlyTheOwnersQuestAndGrantsGoodsExactlyOnce()
    {
        Accept();
        using var start = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(QuestBase), nameof(QuestBase.StartQuest)));
        Commission();
        var amount = QuestOf(Owner)._selectedItemObjectCount;
        WithPlayer(Server, () => Assert.Equal(amount, MobileParty.MainParty.ItemRoster.GetItemNumber(Item(Server))));
        environment.FlushCoalescer();
        WithPlayer(Owner, () => Assert.Equal(amount, MobileParty.MainParty.ItemRoster.GetItemNumber(Item(Owner))));
        Assert.Equal(1, start.CountFor(Owner));
        Assert.Equal(0, start.CountFor(Server));
        Commission();
        WithPlayer(Server, () => Assert.Equal(amount, MobileParty.MainParty.ItemRoster.GetItemNumber(Item(Server))));
        Other.Call(() => Assert.Null(IssueOf(Other).IssueQuest));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void BuyingChecksTheOwnersGoldBeforeGrantingGoods(bool canPay, bool completes)
    {
        Accept();
        var price = QuestOf(Owner)._targetDenarsToAchieve;
        SetGold(canPay ? price + 10 : price - 1);
        WithPlayer(Owner, () =>
        {
            QuestOf(Owner).StartQuest();
            QuestOf(Owner).QuestFinishedPlayerBoughtTheGoods();
        });
        Assert.Equal(completes ? 1 : 0, Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>().Count());
        WithPlayer(Server, () => Assert.Equal(canPay ? 10 : price - 1, Hero.MainHero.Gold));
        if (completes) AssertFinished(IssueOutcome.QuestSuccess);
        else foreach (var instance in Instances) instance.Call(() => Assert.True(IssueOf(instance).IsOngoingWithoutQuest));
    }

    [Fact]
    public void PurchaseRefusedForChangedServerGoldRestoresAnOfferThatCanBeTakenOnCommission()
    {
        Accept();
        var quest = QuestOf(Owner);
        var price = quest._targetDenarsToAchieve;
        WithPlayer(Owner, () => Hero.MainHero.Gold = price + 50);
        WithPlayer(Server, () => Hero.MainHero.Gold = price - 1);
        WithPlayer(Owner, () =>
        {
            quest.StartQuest();
            quest.QuestFinishedPlayerBoughtTheGoods();
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkArtOfTradePurchaseRejected>());
        Assert.True(quest.IsFinalized);
        foreach (var instance in Instances) instance.Call(() =>
        {
            Assert.True(IssueOf(instance).IsOngoingWithoutQuest);
            Assert.Null(IssueOf(instance).IssueQuest);
        });
        Accept();
        Commission();
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        WithPlayer(Server, () => Assert.Equal(QuestOf(Server)._selectedItemObjectCount,
            MobileParty.MainParty.ItemRoster.GetItemNumber(Item(Server))));
    }

    [Fact]
    public void ProfitableReturnKeepsVanillasCappedPaymentAfterTheOwnerSpendsProceeds()
    {
        Accept();
        Commission();
        var quest = QuestOf(Owner);
        WithPlayer(Owner, () => quest.OnPlayerInventoryExchange(new(), new()
        {
            (new ItemRosterElement(Item(Owner), 1), quest._targetDenarsToAchieve),
        }, true));
        Assert.True(quest.ItemCountLeftToSell > 0);
        SetGold(10);
        WithPlayer(Owner, () => quest.QuestSuccessPlayerSoldTheProducts());
        AssertFinished(IssueOutcome.QuestSuccess);
        WithPlayer(Server, () => Assert.Equal(0, Hero.MainHero.Gold));
    }

    [Fact]
    public void ZeroProceedsCanBreakTheAgreementWithoutAPayment()
    {
        Accept();
        Commission();
        Sell(0);
        SetGold(0);
        WithPlayer(Owner, () => QuestOf(Owner).QuestFailedPlayerBrokeTheAgreement());
        AssertFinished(IssueOutcome.QuestFail);
        WithPlayer(Server, () => Assert.Equal(0, Hero.MainHero.Gold));
    }

    [Fact]
    public void CompensationCanBeRetriedAfterTheOwnerRaisesEnoughGold()
    {
        Accept();
        Commission();
        Sell(10);
        SetGold(0);
        WithPlayer(Owner, () => QuestOf(Owner).PlayerPaidAgreedDenarsQuestSuccess());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        SetGold(QuestOf(Owner)._targetDenarsToAchieve);
        WithPlayer(Owner, () => QuestOf(Owner).PlayerPaidAgreedDenarsQuestSuccess());
        AssertFinished(IssueOutcome.QuestSuccess);
    }

    [Theory]
    [InlineData((byte)ArtOfTradeBranch.PaySaleProceeds, true, IssueOutcome.QuestSuccess)]
    [InlineData((byte)ArtOfTradeBranch.PayAgreedSum, false, IssueOutcome.QuestSuccess)]
    [InlineData((byte)ArtOfTradeBranch.BreakAgreement, false, IssueOutcome.QuestFail)]
    public void SaleChoicesRunConsequencesOnTheServerAndReturnTheFinalJournal(byte branch, bool profitable, IssueOutcome expected)
    {
        Accept();
        Commission();
        var price = QuestOf(Owner)._targetDenarsToAchieve;
        SetGold(price + 100);
        Sell(profitable ? price + 25 : price / 2);
        var ownerQuest = QuestOf(Owner);
        var serverQuest = QuestOf(Server);
        WithPlayer(Owner, () => RunChoice(ownerQuest, (ArtOfTradeBranch)branch));

        AssertFinished(expected);
        var payment = profitable || (ArtOfTradeBranch)branch == ArtOfTradeBranch.PayAgreedSum ? price : price / 2;
        WithPlayer(Server, () => Assert.Equal(price + 100 - payment, Hero.MainHero.Gold));
        Assert.Equal(serverQuest.JournalEntries.Last().LogText.Value, ownerQuest.JournalEntries.Last().LogText.Value);
        Assert.Equal(expected == IssueOutcome.QuestSuccess ? 5 : -10 - ((price - payment) / 10), serverQuest.RelationshipChangeWithQuestGiver);
        Other.Call(() => Assert.Empty(Campaign.Current.QuestManager.Quests.OfType<Quest>()));
    }

    [Fact]
    public void PartialSalesAndLostGoodsKeepTheOwnersProgressAndAllowReturningLater()
    {
        Accept();
        Commission();
        var quest = QuestOf(Owner);
        var amount = quest._selectedItemObjectCount;
        WithPlayer(Owner, () => quest.OnPlayerInventoryExchange(new(), new()
        {
            (new ItemRosterElement(Item(Owner), 1), 10),
        }, true));
        Assert.Equal(1, quest._soldItemAmountLog.CurrentProgress);
        Assert.Equal(10, quest._gatheredDenarsLog.CurrentProgress);
        Assert.Equal(1, QuestOf(Server)._soldCount);
        WithPlayer(Server, () =>
        {
            using var allowed = new AllowedThread();
            MobileParty.MainParty.ItemRoster.Clear();
        });
        WithPlayer(Owner, () =>
        {
            using (new AllowedThread()) MobileParty.MainParty.ItemRoster.Clear();
            quest.HourlyTick();
        });
        Assert.True(quest._underSoldLogAdded);
        Assert.Equal(1, quest._daysPassed);
        Assert.Equal(1, QuestOf(Server)._daysPassed);
        SetGold(quest._targetDenarsToAchieve);
        WithPlayer(Owner, () => quest.PlayerPaidAgreedDenarsQuestSuccess());
        AssertFinished(IssueOutcome.QuestSuccess);
        Assert.Contains(quest.JournalEntries, log => log.LogText?.Value?.StartsWith("{=IfPnl0B6}") == true);
    }

    [Fact]
    public void AnotherPlayersProgressAndStaleIssueIdAreIgnored()
    {
        Accept();
        Commission();
        Other.Call(() => Other.Resolve<Common.Network.INetwork>().SendAll(
            new RequestArtOfTradeProgress(scenario.NotableId, QuestScenario.IssueId, 99, 999, 1)));
        Owner.Call(() => Owner.Resolve<Common.Network.INetwork>().SendAll(
            new RequestArtOfTradeProgress(scenario.NotableId, "old-issue", 99, 999, 1)));
        Assert.Equal(0, QuestOf(Server)._soldCount);
        Assert.Equal(0, QuestOf(Server)._gatheredDenars);
    }

    [Fact]
    public void TimeoutIsReplayedWithTheOwnersFinalJournal()
    {
        Accept();
        Commission();
        var quest = QuestOf(Owner);
        WithPlayer(Owner, () => quest.CompleteQuestWithTimeOut());
        AssertFinished(IssueOutcome.QuestTimeOut);
        Assert.Contains(quest.JournalEntries, log => log.LogText?.Value?.StartsWith("{=PQt8lfTX}") == true);
    }

    [Fact]
    public void ServerRaidCancelsEveryCopy()
    {
        Accept();
        Commission();
        Server.Call(() => CampaignEventDispatcher.Instance.OnVillageBeingRaided(Get<Village>(Server, villageId)), NativePresentation);
        AssertFinished(IssueOutcome.QuestCancel);
    }

    [Fact]
    public void ClientRaidReplayLeavesTheDecisionToTheServer()
    {
        Accept();
        Commission();
        Owner.Call(() => QuestOf(Owner).OnVillageRaided(Get<Village>(Owner, villageId)));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.True(QuestOf(Owner).IsOngoing);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FrameworkAllowsAnotherPlayersCopyAndRefusesTheSamePlayersSecondCopy(bool otherPlayer)
    {
        Accept();
        Commission();
        var secondGiverId = environment.CreateRegisteredObject<Hero>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using var allowed = new AllowedThread();
                var giver = Get<Hero>(instance, secondGiverId);
                giver.StayingInSettlement = Get<Settlement>(instance, settlementId);
                giver.Occupation = Occupation.RuralNotable;
                giver.Clan = Get<Clan>(instance, giverClanId);
                var issue = new Issue(giver, Item(instance)) { StringId = "second_trade" };
                issue._issueState = IssueBase.IssueState.Ongoing;
                Campaign.Current.IssueManager._issues.Add(giver, issue);
                giver.OnIssueCreatedForHero(issue);
            });
        }

        var acceptingClient = otherPlayer ? Other : Owner;
        WithPlayer(acceptingClient, () => Get<Hero>(acceptingClient, secondGiverId).Issue.StartIssueWithQuest(),
            otherPlayer ? QuestScenario.ControllerB : QuestScenario.ControllerA);

        Server.Call(() =>
        {
            var owned = Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(secondGiverId, "second_trade", out var controller);
            Assert.Equal(otherPlayer, owned);
            if (otherPlayer) Assert.Equal(QuestScenario.ControllerB, controller);
        });
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        Assert.True(QuestOf(Owner).IsOngoing);
    }

    [Fact]
    public void RaidEligibilityIsCheckedOnTheServerWithoutAQuestSpecificGate()
    {
        Server.Call(() =>
        {
            using var allowed = new AllowedThread();
            Get<Village>(Server, villageId).VillageState = Village.VillageStates.Looted;
        });
        Accept();
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
    }

    [Theory]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByRebellion, IssueOutcome.QuestCancel)]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility, IssueOutcome.QuestFail)]
    public void WarRunsTheVanillaFailureOrCancellationAsTheOwner(DeclareWarAction.DeclareWarDetail detail, IssueOutcome expected)
    {
        Accept();
        Commission();
        Server.Call(() => DeclareWarAction.ApplyInternal(Get<Kingdom>(Server, kingdomId), Get<Clan>(Server, playerClanId), detail), NativePresentation);
        AssertFinished(expected);
        Assert.Empty(Owner.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
    }

    [Fact]
    public void KingdomChangeIntoWarCancelsTheOwnersQuest()
    {
        Accept();
        Commission();
        Server.Call(() =>
        {
            var kingdom = Get<Kingdom>(Server, kingdomId);
            var clan = Get<Clan>(Server, playerClanId);
            using (new AllowedThread()) FactionManager.DeclareWar(kingdom, clan);
            CampaignEventDispatcher.Instance.OnClanChangedKingdom(clan, null, kingdom,
                ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, false);
        }, NativePresentation);
        AssertFinished(IssueOutcome.QuestCancel);
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.IsForcingSupplies, true)]
    [InlineData(MapEvent.BattleTypes.IsForcingVolunteers, true)]
    [InlineData(MapEvent.BattleTypes.IsForcingSupplies, false)]
    public void CoercionOfTheGiversVillageFailsOnlyForTheQuestOwner(MapEvent.BattleTypes kind, bool ownerAttacks)
    {
        Accept();
        Commission();
        var battleId = environment.CreateRegisteredObject<MapEvent>();
        Server.Call(() =>
        {
            var village = Get<Settlement>(Server, settlementId);
            var battle = Get<MapEvent>(Server, battleId);
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(ownerAttacks ? QuestScenario.ControllerA : QuestScenario.ControllerB, out var attacker));
            var party = Get<MobileParty>(Server, attacker.MobilePartyId).Party;
            using (new AllowedThread())
            {
                village._notablesCache.Add(Giver(Server));
                village.Village.Bound.Town._ownerClan = Get<Clan>(Server, giverClanId);
                battle._mapEventType = kind;
                battle.MapEventSettlement = village;
            }
            CampaignEventDispatcher.Instance.OnMapEventStarted(battle, party, village.Party);
        }, NativePresentation);
        if (ownerAttacks) AssertFinished(IssueOutcome.QuestFail);
        else
        {
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
            Assert.True(QuestOf(Owner).IsOngoing);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CompanionDispatchAndInheritedCompletionKeepTheRewardWithTheOwner(bool fails, bool cancels)
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using var allowed = new AllowedThread();
                Get<CharacterObject>(instance, troopId).Level = 16;
                Get<Hero>(instance, companionId).SetSkillValue(DefaultSkills.Trade, 120);
            });
        }

        WithPlayer(Owner, () =>
        {
            var issue = IssueOf(Owner);
            using (new AllowedThread())
            {
                issue.AlternativeSolutionSentTroops.AddToCounts(Get<Hero>(Owner, companionId).CharacterObject, 1);
                issue.AlternativeSolutionSentTroops.AddToCounts(Get<CharacterObject>(Owner, troopId), 10);
            }
            Owner.Resolve<Common.Messaging.IMessageBroker>().Publish(this, new AlternativeSolutionAcceptRequested(issue));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        var serverIssue = IssueOf(Server);
        var ownerIssue = IssueOf(Owner);
        Server.Call(() => Campaign.Current.IssueManager.DailyTick(), NativePresentation);
        Assert.Equal(serverIssue.JournalEntries[0].CurrentProgress, ownerIssue.JournalEntries[0].CurrentProgress);
        Assert.True(ownerIssue.JournalEntries[0].CurrentProgress > 0);
        int reward = 0;
        int otherGold = 0;
        WithPlayer(Server, () =>
        {
            Hero.MainHero.Gold = 0;
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerB, out var other));
            otherGold = Get<Hero>(Server, other.HeroId).Gold;
            reward = serverIssue.RewardGold;
            serverIssue._failureChance = fails ? 1f : -1f;
            if (cancels) serverIssue.CompleteIssueWithCancel();
            else serverIssue.CompleteIssueWithAlternativeSolution();
        });

        AssertFinished(IssueOutcome.AlternativeSolution);
        Assert.Equal(fails || cancels ? 0 : 5, serverIssue.RelationshipChangeWithIssueOwner);
        Assert.Empty(serverIssue.AlternativeSolutionSentTroops.GetTroopRoster());
        Assert.Equal(serverIssue.JournalEntries.Last().LogText.Value, ownerIssue.JournalEntries.Last().LogText.Value);
        WithPlayer(Server, () =>
        {
            Assert.Equal(fails || cancels ? 0 : reward, Hero.MainHero.Gold);
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerB, out var other));
            Assert.Equal(otherGold, Get<Hero>(Server, other.HeroId).Gold);
        });
    }

    [Fact]
    public void AiLordResolutionClearsAnUnacceptedOfferOnBothClients()
    {
        var lordId = environment.CreateRegisteredObject<Hero>();
        Server.Call(() => IssueOf(Server).CompleteIssueWithAiLord(Get<Hero>(Server, lordId)));
        AssertFinished(IssueOutcome.QuestCancel);
    }

    [Fact]
    public void ServerIssueCancellationEndsTheOwnersQuestExactlyOnce()
    {
        Accept();
        Commission();
        Server.Call(() => IssueOf(Server).CompleteIssueWithCancel(), NativePresentation);
        AssertFinished(IssueOutcome.QuestCancel);
    }

    private void Accept() => WithPlayer(Owner, () => IssueOf(Owner).StartIssueWithQuest());
    private void Commission() => WithPlayer(Owner, () => QuestOf(Owner).QuestAcceptedConsequences());

    private void Sell(int proceeds)
    {
        WithPlayer(Owner, () =>
        {
            var quest = QuestOf(Owner);
            quest.OnPlayerInventoryExchange(new(), new()
            {
                (new ItemRosterElement(Item(Owner), quest._selectedItemObjectCount), proceeds),
            }, true);
        });
    }

    private void SetGold(int gold)
    {
        foreach (var instance in new[] { Server, Owner }) WithPlayer(instance, () => Hero.MainHero.Gold = gold);
    }

    private void AssertFinished(IssueOutcome expected)
    {
        Assert.Equal(expected, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        foreach (var instance in Instances) instance.Call(() => Assert.Null(Giver(instance).Issue));
    }

    private void WithPlayer(EnvironmentInstance instance, Action action, string controllerId = QuestScenario.ControllerA)
    {
        instance.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(controllerId, out var player));
            var hero = Get<Hero>(instance, player.HeroId);
            using var scope = new MainHeroSubstitutionScope(hero, Get<MobileParty>(instance, player.MobilePartyId));
            action();
        }, NativePresentation);
    }

    private void Configure(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            using var allowed = new AllowedThread();
            var giver = Giver(instance);
            var settlement = Get<Settlement>(instance, settlementId);
            var village = Get<Village>(instance, villageId);
            settlement.SetSettlementComponent(village);
            var bound = Get<Settlement>(instance, boundSettlementId);
            var town = Get<Town>(instance, boundTownId);
            bound.SetSettlementComponent(town);
            village.Bound = bound;
            var production = (VillageType)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(VillageType));
            production._productions = new() { (Item(instance), 1f) };
            village.VillageType = production;
            Item(instance).Value = 100;
            giver.StayingInSettlement = settlement;
            giver.Occupation = Occupation.RuralNotable;
            var clan = Get<Clan>(instance, giverClanId);
            town._ownerClan = clan;
            town.IsOwnerUnassigned = false;
            clan._kingdom = Get<Kingdom>(instance, kingdomId);
            giver.Clan = clan;
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(QuestScenario.ControllerA, out var owner));
            Get<Hero>(instance, owner.HeroId).Clan = Get<Clan>(instance, playerClanId);
            Get<Hero>(instance, owner.HeroId).PartyBelongedTo = Get<MobileParty>(instance, owner.MobilePartyId);
            var issue = new Issue(giver, Item(instance)) { StringId = QuestScenario.IssueId };
            issue._issueState = IssueBase.IssueState.Ongoing;
            Campaign.Current.IssueManager._issues[giver] = issue;
            giver.OnIssueCreatedForHero(issue);
        });
    }

    private static void RunChoice(Quest quest, ArtOfTradeBranch branch)
    {
        switch (branch)
        {
            case ArtOfTradeBranch.PaySaleProceeds: quest.QuestSuccessPlayerSoldTheProducts(); break;
            case ArtOfTradeBranch.PayAgreedSum: quest.PlayerPaidAgreedDenarsQuestSuccess(); break;
            case ArtOfTradeBranch.BreakAgreement: quest.QuestFailedPlayerBrokeTheAgreement(); break;
        }
    }

    private Hero Giver(EnvironmentInstance instance) => Get<Hero>(instance, scenario.NotableId);
    private Issue IssueOf(EnvironmentInstance instance) => Assert.IsType<Issue>(Giver(instance).Issue);
    private Quest QuestOf(EnvironmentInstance instance) => Assert.IsType<Quest>(IssueOf(instance).IssueQuest);
    private ItemObject Item(EnvironmentInstance instance) => Get<ItemObject>(instance, itemId);
    private static T Get<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }

    private static IQuestTypeDescriptor Descriptor(EnvironmentInstance instance)
    {
        Assert.True(instance.Resolve<IQuestTypeRegistry>().TryGet(typeof(Issue), out var descriptor));
        return descriptor;
    }
}
