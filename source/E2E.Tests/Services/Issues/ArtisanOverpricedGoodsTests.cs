using Common.Messaging;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Connections.Messages;
using Common;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Registries;
using GameInterface.Services.UI.Notifications;
using GameInterface.Services.Issues.Quests.ArtisanOverpricedGoods;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
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
    public void LordStart_UsesTheRequestingPlayersDifficulty()
    {
        ClientA.Call(() =>
        {
            using (new IssueDifficultyOverride(2f))
                Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution();
        });

        Assert.Equal(2f, Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestLordSolution>()).Difficulty);
        Assert.Equal(2f, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLordSolutionStarted>()).Difficulty);
        foreach (var instance in Instances)
            instance.Call(() => Assert.Equal(2f, Get<Hero>(instance, scenario.NotableId).Issue._issueDifficultyMultiplier));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoCounterOffer_MenuReplayRequestsTheServerAndDoesNotCompleteLocally(bool influenceDropped)
    {
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        // Exercise IssueBase's no-counteroffer menu branch on the owner; retain the real artisan's
        // merchant on the server because its reward consequence changes that merchant's power.
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.CounterOfferHero = null);
        if (influenceDropped)
            Server.Call(() =>
            {
                Get<Hero>(Server, scenario.NotableId).Issue.CounterOfferHero = null;
                Get<Clan>(Server, clanId).Influence = 0;
            });
        environment.FlushCoalescer();
        // The framework sees a no-counteroffer issue; the actual artisan reward still needs its merchant.
        var harmony = new Harmony($"no-counteroffer-rewards.{Guid.NewGuid():N}");
        harmony.Patch(AccessTools.Method(typeof(Issue), nameof(Issue.LordSolutionConsequenceWithRefuseCounterOffer)),
            prefix: new HarmonyMethod(typeof(ArtisanOverpricedGoodsTests), nameof(RestoreRewardMerchant)));
        try
        {
            OnMapAfterConversation(ClientA, () =>
            {
                using (new AllowedThread())
                    CampaignEventDispatcher.Instance.BeforeGameMenuOpened(null);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
        environment.FlushCoalescer();

        Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestLordSolution>(),
            request => request.Operation == LordSolutionOperation.RefuseCounterOffer);
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        AssertFinished(2530, 10900);
        AssertEffects(210, 90, 5, -10);
    }

    private static void RestoreRewardMerchant(Issue __instance)
    {
        if (ModInformation.IsServer)
            __instance.CounterOfferHero = __instance.IssueSettlement.Notables.First(hero => hero.IsMerchant);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LordCompletion_ArchivesOnlyTheOwnerIncludingTheDownloadedSave(bool counterOffer)
    {
        QuestJournalHistoryAssertions.RegisterJournalBehavior(Instances);
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        var ownerIssue = Get<Hero>(ClientA, scenario.NotableId).Issue;
        ClientA.Call(() =>
        {
            if (counterOffer) ownerIssue.CompleteIssueWithLordSolutionWithAcceptCounterOffer();
            else ownerIssue.CompleteIssueWithLordSolutionWithRefuseCounterOffer();
        });
        environment.FlushCoalescer();

        ClientA.Call(() =>
        {
            var entry = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                log => log.IsRelatedTo(ownerIssue)));
            Assert.True(entry.IsEnded());
            Assert.Equal(counterOffer, entry.IsEndedUnsuccessfully());
        });
        LogEntry[] history = null!;
        Server.Call(() =>
        {
            history = Campaign.Current.LogEntryHistory.GameActionLogs.ToArray();
            var entry = Assert.IsType<PlayerJournalLogEntry>(Assert.Single(history.OfType<JournalLogEntry>()));
            Assert.Equal(QuestScenario.ControllerA, entry.OwnerControllerId);
            Assert.True(entry.IsEnded());
        });
        ClientB.Call(() =>
        {
            Campaign.Current.LogEntryHistory._logs.Clear();
            Campaign.Current.LogEntryHistory._logs.AddRange(history);
            Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(entry => entry.IsEnded()));
        });
    }

    [Fact]
    public void LordOwnershipSnapshot_NormalizesTheLoadedSolverFlag()
    {
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        ClientB.Call(() => Get<Hero>(ClientB, scenario.NotableId).Issue.IsTriedToSolveBefore = true);
        ClientB.SimulateMessage(Server.NetPeer, new NetworkIssueOwnershipSnapshot(new[]
        {
            new IssueOwnershipData(scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA),
        }));
        ClientB.Call(() => Assert.False(Get<Hero>(ClientB, scenario.NotableId).Issue.IsTriedToSolveBefore));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LordCompletion_NormalizesTheLoadedNonOwnerBeforeOrAfterItsSnapshot(bool completesBeforeSnapshot)
    {
        QuestJournalHistoryAssertions.RegisterJournalBehavior(Instances);
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        var ownerIssue = Get<Hero>(ClientA, scenario.NotableId).Issue;
        var observerIssue = Get<Hero>(ClientB, scenario.NotableId).Issue;
        IssueBase.IssueState savedState = default;
        var savedTried = false;
        var savedLog = "";
        Server.Call(() =>
        {
            var savedIssue = Get<Hero>(Server, scenario.NotableId).Issue;
            savedState = savedIssue._issueState;
            savedTried = savedIssue.IsTriedToSolveBefore;
            savedLog = Assert.Single(savedIssue.JournalEntries).LogText.ToString();
        });
        Assert.True(savedTried);
        ClientB.Call(() =>
        {
            // A joining client loads the server's pending issue before its ownership snapshot arrives.
            ClientB.Resolve<IIssueOwnershipRegistry>().Remove(scenario.NotableId);
            observerIssue._issueState = savedState;
            observerIssue.IsTriedToSolveBefore = savedTried;
            using (new AllowedThread())
                observerIssue.AddLog(new JournalLog(CampaignTime.Now, new TextObject(savedLog)));
        });
        if (!completesBeforeSnapshot)
            Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new PlayerCampaignEntered(ClientB.NetPeer)));

        ClientA.Call(() => ownerIssue.CompleteIssueWithLordSolutionWithRefuseCounterOffer());
        if (completesBeforeSnapshot)
            Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new PlayerCampaignEntered(ClientB.NetPeer)));
        environment.FlushCoalescer();

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        ClientA.Call(() =>
        {
            Assert.True(ownerIssue.IsTriedToSolveBefore);
            Assert.Equal(2, ownerIssue.JournalEntries.Count);
            Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry.IsRelatedTo(ownerIssue) && entry.IsEnded()));
        });
        ClientB.Call(() =>
        {
            Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(entry => entry.IsEnded()));
            Assert.False(observerIssue.IsTriedToSolveBefore);
            Assert.Null(Get<Hero>(ClientB, scenario.NotableId).Issue);
        });
    }

    [Fact]
    public void PendingLordCancellation_TransportsTheLocalizedEndingOnlyToItsOwner()
    {
        QuestJournalHistoryAssertions.RegisterJournalBehavior(Instances);
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        var ownerIssue = Get<Hero>(ClientA, scenario.NotableId).Issue;
        var observerIssue = Get<Hero>(ClientB, scenario.NotableId).Issue;
        var expected = "";
        Server.Call(() =>
        {
            var issue = Get<Hero>(Server, scenario.NotableId).Issue;
            var owner = Get<Hero>(Server, scenario.OwnerHeroId);
            var text = new TextObject("{=rTvWdMXF}{DIED_HERO.LINK} died and your agreement with {?DIED_HERO.GENDER}her{?}him{\\?} canceled.");
            StringHelpers.SetCharacterProperties("DIED_HERO", issue.IssueOwner.CharacterObject, text);
            expected = text.ToString();
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
            {
                issue.CompleteIssueWithCancel(text);
                Server.Resolve<IOutcomeDispatcher>().After(issue, IssueOutcome.QuestCancel);
            }
            Assert.Equal(2, issue.JournalEntries.Count);
            Assert.Single(issue.JournalEntries, log => log.LogText.ToString() == expected);
        });
        environment.FlushCoalescer();

        Assert.Equal(IssueOutcome.QuestCancel,
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        AssertFinished(2500, 10000);
        AssertEffects(200, 100, 0, 0);
        Server.Call(() => Assert.Equal(100, Get<Clan>(Server, clanId).Influence));
        ClientA.Call(() =>
        {
            Assert.Equal(2, ownerIssue.JournalEntries.Count);
            Assert.Single(ownerIssue.JournalEntries, log => log.LogText.ToString() == expected);
            var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry.IsRelatedTo(ownerIssue)));
            Assert.True(archive.IsEnded());
            Assert.Single(archive.GetEntries(), log => log.LogText.ToString() == expected);
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueJournal>());
        ClientB.Call(() =>
        {
            Assert.Empty(observerIssue.JournalEntries);
            Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(entry => entry.IsEnded()));
        });
    }

    [Fact]
    public void StaleDirectAccept_DoesNotReplaceAnAcceptedLordSolution()
    {
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        using var notifications = new MethodCallRecorder(
            AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation)));
        ClientA.Call(() => Assert.True(Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithQuest()));

        Assert.Equal(1, notifications.CountFor(ClientA));
        Assert.Equal(0, notifications.CountFor(ClientB));
        Assert.Equal(0, notifications.CountFor(Server));
        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestSolutionAccept>());
        ClientA.Call(() =>
        {
            var issue = Get<Hero>(ClientA, scenario.NotableId).Issue;
            Assert.True(issue.IsSolvingWithLordSolution);
            Assert.Null(issue.IssueQuest);
        });
    }

    [Fact]
    public void RefusedDirectAccept_RestoresTheServersOriginalOfferDeadline()
    {
        CampaignTime due = default;
        Server.Call(() => due = CampaignTime.DaysFromNow(7));
        foreach (var instance in Instances)
            instance.Call(() => Get<Hero>(instance, scenario.NotableId).Issue.IssueDueTime = due);
        Server.Call(() => ((ArtisanPriceModel)Campaign.Current.Models.TradeItemPriceFactorModel).PriceIndex = 1f);
        Accept();

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        ClientA.Call(() => Assert.Equal(due, Get<Hero>(ClientA, scenario.NotableId).Issue.IssueDueTime));
    }

    [Fact]
    public void LordAcceptance_RemovesALosingOptimisticQuestBeforeItsRejectionArrives()
    {
        QuestJournalHistoryAssertions.RegisterJournalBehavior(Instances);
        foreach (var instance in Instances)
            instance.Call(() => SetupPlayer(instance, scenario.OtherHeroId, scenario.OtherPartyId, clanId));
        var router = Server.Resolve<TestNetworkRouter>();
        router.PauseLink(ClientA.NetPeer, Server.NetPeer);
        Accept();
        var strayQuest = scenario.QuestOf(ClientA);
        ClientB.Call(() => Get<Hero>(ClientB, scenario.NotableId).Issue.StartIssueWithLordSolution());

        ClientA.Call(() =>
        {
            var issue = Get<Hero>(ClientA, scenario.NotableId).Issue;
            Assert.True(issue.IsSolvingWithLordSolution);
            Assert.Null(issue.IssueQuest);
            Assert.True(strayQuest.IsFinalized);
            Assert.DoesNotContain(strayQuest, Campaign.Current.QuestManager.Quests);
            Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(log => log.IsRelatedTo(issue)));
        });
        router.ResumeLink(ClientA.NetPeer, Server.NetPeer);
        environment.FlushCoalescer();
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Equal(QuestScenario.ControllerB, scenario.OwnerOnServer());
    }

    [Fact]
    public void Acceptance_StartsOnlyTheOwnersQuestAndRetainsAnUnstartedServerCopy()
    {
        Accept();
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        ClientA.Call(() => Assert.True(QuestHere(ClientA).IsOngoing));
        Server.Call(() => Assert.Null(QuestHere(Server)._playerStartsQuestLog));
        ClientB.Call(() => Assert.Null(Get<Hero>(ClientB, scenario.NotableId).Issue.IssueQuest));
    }

    [Fact]
    public void CreationCapture_PreservesTheServersMerchantMaterialAmountAndPayment()
    {
        byte[] captured = null!;
        Server.Call(() => captured = Descriptor(Server).CreationCaptureStrategy.Capture(Get<Hero>(Server, scenario.NotableId).Issue));
        ClientA.Call(() =>
        {
            using (new AllowedThread())
            {
                var issue = (Issue)Descriptor(ClientA).CreationCaptureStrategy.CreateIssue(Get<Hero>(ClientA, scenario.NotableId), captured);
                Assert.Same(Get<Hero>(ClientA, merchantId), issue.CounterOfferHero);
                Assert.Same(Get<ItemObject>(ClientA, itemId), issue._requestedTradeGood);
                Assert.Equal(Amount, issue.RequestedTradeGoodAmount);
                Assert.Equal(Payment, issue._goldReward);
            }
        });
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
    public void ReceivedDeliveryProgressDoesNotSendTheServerItsOwnJournalBack()
    {
        Accept();
        environment.FlushCoalescer();
        var before = ClientA.NetworkSentMessages.GetMessages<RequestQuestJournal>().Count();
        ClientA.SimulateMessage(Server.NetPeer,
            new NetworkArtisanDeliveryProgress(scenario.NotableId, QuestScenario.IssueId, 2));
        ClientA.Call(() => Assert.Equal(2, QuestHere(ClientA)._playerStartsQuestLog.CurrentProgress));
        Assert.Equal(before, ClientA.NetworkSentMessages.GetMessages<RequestQuestJournal>().Count());
    }

    [Fact]
    public void AuthoritativeEndingPreservesOwnerDeliveryProgress()
    {
        Accept();
        var ownerQuest = QuestHere(ClientA);
        ClientA.SimulateMessage(Server.NetPeer,
            new NetworkArtisanDeliveryProgress(scenario.NotableId, QuestScenario.IssueId, 2));
        ClientA.Call(() => Assert.Equal(2, ownerQuest._playerStartsQuestLog.CurrentProgress));

        Server.Call(() => QuestHere(Server).CompleteQuestWithCancel());
        environment.FlushCoalescer();

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueJournal>());
        ClientA.Call(() =>
        {
            Assert.True(ownerQuest.IsFinalized);
            Assert.Equal(2, ownerQuest._playerStartsQuestLog.CurrentProgress);
        });
    }

    [Fact]
    public void PartialThenFullDelivery_UpdatesOnlyTheOwnerAndPaysExactlyOnce()
    {
        Accept();
        Stock(2);
        ClientA.Call(() => QuestHere(ClientA).DeliverItemsPartiallyOnConsequence());
        Assert.Single(ClientA.InternalMessages.GetMessages<NetworkArtisanDeliveryProgress>());
        Assert.Empty(ClientB.InternalMessages.GetMessages<NetworkArtisanDeliveryProgress>());
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
        AssertEffects(210, 90, 5, -10);
        Server.Call(() => Assert.Equal(0, Get<MobileParty>(Server, scenario.OwnerPartyId).ItemRoster.GetItemNumber(Get<ItemObject>(Server, itemId))));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompletedQuestDoesNotLeakThroughTheLateJoinSaveHistory(bool success)
    {
        QuestJournalHistoryAssertions.RegisterJournalBehavior(Instances);

        Accept();
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        var ownerQuest = (Quest)scenario.QuestOf(ClientA);
        if (success)
        {
            Stock(Amount);
            ClientA.Call(() => ownerQuest.DeliverItemsFullyOnConsequence());
            ClientA.Call(() => ownerQuest.CompleteQuestWithSuccess());
        }
        else
        {
            ClientA.Call(() => ownerQuest.CompleteQuestWithFail());
        }

        QuestJournalHistoryAssertions.AssertLateJoinHistory(Server, ClientA, ClientB, QuestScenario.IssueId, ownerQuest,
            success ? IssueOutcome.QuestSuccess : IssueOutcome.QuestFail);
    }

    [Fact]
    public void FailedAcceptDoesNotLeaveTheNextPlayersArchiveOwnedByTheRejectedPlayer()
    {
        QuestJournalHistoryAssertions.RegisterJournalBehavior(new[] { Server, ClientB });
        var harmony = new Harmony($"{nameof(FailedAcceptDoesNotLeaveTheNextPlayersArchiveOwnedByTheRejectedPlayer)}.{Guid.NewGuid():N}");
        var target = AccessTools.Method(typeof(ArtisanOverpricedGoodsQuestSolutionAcceptStrategy),
            nameof(ArtisanOverpricedGoodsQuestSolutionAcceptStrategy.Apply));
        harmony.Patch(target, prefix: new HarmonyMethod(typeof(ArtisanOverpricedGoodsTests), nameof(ThrowOnServerAccept)));
        try
        {
            Accept();
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Server.Call(() => Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
            entry => true)));

        ClientB.Call(() => StartQuest(ClientB, scenario.NotableId));
        Assert.Equal(QuestScenario.ControllerB, scenario.OwnerOnServer());
        var ownerQuest = (Quest)scenario.QuestOf(ClientB);
        Server.Call(() => Get<MobileParty>(Server, scenario.OtherPartyId).ItemRoster.AddToCounts(Get<ItemObject>(Server, itemId), Amount));
        environment.FlushCoalescer();
        ClientB.Call(() => ownerQuest.DeliverItemsFullyOnConsequence());
        ClientB.Call(() => ownerQuest.CompleteQuestWithSuccess());
        QuestJournalHistoryAssertions.AssertLateJoinHistory(Server, ClientB, ClientA, QuestScenario.IssueId, ownerQuest, IssueOutcome.QuestSuccess);
    }

    private static void ThrowOnServerAccept()
    {
        if (ModInformation.IsServer)
            throw new InvalidOperationException("Stubbed accept failure");
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
        AssertEffects(190, 105, -5, 5);
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
        AssertEffects(cancel ? 200 : 180, 100, cancel ? 0 : -5, 0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void LordCounterOffer_ChargesAndRewardsOnlyTheOwner(bool acceptMerchant, bool leaveTown)
    {
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLordSolutionStarted>());
        var ownerIssue = Get<Hero>(ClientA, scenario.NotableId).Issue;
        var observerIssue = Get<Hero>(ClientB, scenario.NotableId).Issue;
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        var cost = 0;
        Server.Call(() => cost = Get<Hero>(Server, scenario.NotableId).Issue.NeededInfluenceForLordSolution);

        if (leaveTown)
        {
            Server.Call(() => Get<MobileParty>(Server, scenario.OwnerPartyId).CurrentSettlement = null);
            environment.FlushCoalescer();
        }

        ClientA.Call(() =>
        {
            var issue = Get<Hero>(ClientA, scenario.NotableId).Issue;
            if (acceptMerchant) issue.CompleteIssueWithLordSolutionWithAcceptCounterOffer();
            else issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer();
        });
        environment.FlushCoalescer();
        var decision = Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestLordSolution>(),
            request => request.Operation != LordSolutionOperation.Start);
        Assert.Equal(acceptMerchant ? LordSolutionOperation.AcceptCounterOffer : LordSolutionOperation.RefuseCounterOffer, decision.Operation);
        Assert.Equal(0f, decision.Difficulty);
        var outcome = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome;
        Assert.Equal(acceptMerchant ? IssueOutcome.LordAcceptedCounterOffer : IssueOutcome.LordRefusedCounterOffer, outcome);
        AssertFinished(acceptMerchant ? 2470 : 2530, acceptMerchant ? 10000 : 10900);
        Assert.Equal(2, ownerIssue.JournalEntries.Count);
        Assert.Empty(observerIssue.JournalEntries);
        AssertEffects(acceptMerchant ? 195 : 210, acceptMerchant ? 100 : 90, acceptMerchant ? -5 : 5, acceptMerchant ? 5 : -10);
        Server.Call(() => Assert.Equal(acceptMerchant ? 100 : 100 - cost, Get<Clan>(Server, clanId).Influence));
        ClientA.Call(() => Assert.False(ClientA.Resolve<ILordSolutionCoordinator>().IsDecisionPending(ownerIssue)));
    }

    [Fact]
    public void LordSolution_CannotStartAfterLeavingTheArtisansTown()
    {
        Server.Call(() => Get<MobileParty>(Server, scenario.OwnerPartyId).CurrentSettlement = null);
        environment.FlushCoalescer();
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkLordSolutionStarted>());
        Assert.Single(Server.NetworkSentMessages.GetMessages<SendInformationMessage>());
        Server.Call(() => Assert.True(Get<Hero>(Server, scenario.NotableId).Issue.IsOngoingWithoutQuest));
        Server.Call(() => Assert.False(Server.Resolve<IIssueOwnershipRegistry>()
            .TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _)));
    }

    [Fact]
    public void LordCounterOffer_StillRequiresInfluenceToKeepTheDecreeAfterLeavingTown()
    {
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        Server.Call(() =>
        {
            Get<MobileParty>(Server, scenario.OwnerPartyId).CurrentSettlement = null;
            Get<Clan>(Server, clanId).Influence = 0;
        });
        environment.FlushCoalescer();
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer());

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Single(Server.NetworkSentMessages.GetMessages<SendInformationMessage>());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        Server.Call(() =>
        {
            Assert.True(Get<Hero>(Server, scenario.NotableId).Issue.IsSolvingWithLordSolution);
            Assert.Equal(10000, Get<Hero>(Server, scenario.OwnerHeroId).Gold);
        });
    }

    [Fact]
    public void LordCounterOffer_RejectedDecisionCanRetryAfterInfluenceIsRestored()
    {
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        Server.Call(() => Get<Clan>(Server, clanId).Influence = 0);
        environment.FlushCoalescer();

        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer());
        environment.FlushCoalescer();
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLordSolutionRejected>());

        ClientA.Call(() => Assert.False(ClientA.Resolve<ILordSolutionCoordinator>()
            .IsDecisionPending(Get<Hero>(ClientA, scenario.NotableId).Issue)));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());

        Server.Call(() => Get<Clan>(Server, clanId).Influence = 100);
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer());
        environment.FlushCoalescer();

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void ASecondPlayer_CannotCompleteTheFirstPlayersLordSolution()
    {
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        ClientB.Call(() => Get<Hero>(ClientB, scenario.NotableId).Issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Single(Server.NetworkSentMessages.GetMessages<SendInformationMessage>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LordSolution_RefusesUnavailableInfluenceOrAnotherClansTown(bool anotherClan)
    {
        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                if (anotherClan) Get<Town>(Server, townId).OwnerClan = Get<Clan>(Server, otherClanId);
                else Get<Clan>(Server, clanId).Influence = 0;
            }
        });
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkLordSolutionStarted>());
        Assert.Single(Server.NetworkSentMessages.GetMessages<SendInformationMessage>());
        Server.Call(() => Assert.True(Get<Hero>(Server, scenario.NotableId).Issue.IsOngoingWithoutQuest));
    }

    [Fact]
    public void LordStartResponse_RetriesTheVanillaMerchantConversationOnlyForTheOwner()
    {
        using var menu = new MethodCallRecorder(AccessTools.Method(typeof(IssueBase), nameof(IssueBase.BeforeGameMenuOpened)));
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        Assert.Equal(1, menu.CountFor(ClientA));
        Assert.Equal(0, menu.CountFor(ClientB));
        Assert.Equal(0, menu.CountFor(Server));
    }

    [Fact]
    public void LordStartResponse_LeavesTheMerchantForTheMenuAfterTheArtisanConversation()
    {
        using (var menu = new MethodCallRecorder(AccessTools.Method(typeof(IssueBase), nameof(IssueBase.BeforeGameMenuOpened))))
        {
            ClientA.Call(() => Campaign.Current.ConversationManager.IsConversationInProgress = true);
            ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
            ClientA.Call(() => Campaign.Current.ConversationManager.IsConversationInProgress = false);

            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLordSolutionStarted>());
            ClientA.Call(() => Assert.True(Get<Hero>(ClientA, scenario.NotableId).Issue.IsSolvingWithLordSolution));
            Assert.Equal(0, menu.CountFor(ClientA));
        }

        // CampaignMapConversation.OpenConversation is small enough to be inlined, so record one level down.
        using var conversations = new MethodCallRecorder(
            AccessTools.Method(typeof(ConversationManager), nameof(ConversationManager.OpenMapConversation)));
        OnMapAfterConversation(ClientA, () => CampaignEventDispatcher.Instance.BeforeGameMenuOpened(null));

        Assert.Equal(1, conversations.CountFor(ClientA));
    }

    [Fact]
    public void CounterOfferDecision_DoesNotReopenTheMerchantConversationBeforeTheServerReplies()
    {
        ClientA.Call(() => Get<Hero>(ClientA, scenario.NotableId).Issue.StartIssueWithLordSolution());
        // The refusal leaves the owner's copy exactly as it is while a decision is in flight.
        Server.Call(() => Get<Clan>(Server, clanId).Influence = 0);
        environment.FlushCoalescer();
        // CampaignMapConversation.OpenConversation is small enough to be inlined, so record one level down.
        using var conversations = new MethodCallRecorder(
            AccessTools.Method(typeof(ConversationManager), nameof(ConversationManager.OpenMapConversation)));

        OnMapAfterConversation(ClientA, () =>
        {
            var issue = Get<Hero>(ClientA, scenario.NotableId).Issue;
            issue.BeforeGameMenuOpened(null);
            issue.CompleteIssueWithLordSolutionWithRefuseCounterOffer();
            Assert.True(issue.IsSolvingWithLordSolution);
            issue.BeforeGameMenuOpened(null);
        });

        Assert.Equal(1, conversations.CountFor(ClientA));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ServerEnding_ArchivesTheVanillaTextOnceWithoutClientRewards(bool timeout)
    {
        foreach (var instance in Instances)
            instance.Call(() => new JournalLogsCampaignBehavior().RegisterEvents());
        Accept();
        var ownerQuest = (Quest)scenario.QuestOf(ClientA);
        var serverQuest = (Quest)scenario.QuestOf(Server);
        using var gold = new MethodCallRecorder(Priority.First, AccessTools.Method(typeof(TaleWorlds.CampaignSystem.Actions.GiveGoldAction), "ApplyInternal"));
        using var traits = new MethodCallRecorder(Priority.First, AccessTools.Method(typeof(TaleWorlds.CampaignSystem.CharacterDevelopment.TraitLevelingHelper), "OnIssueSolvedThroughQuest", new[] { typeof(Hero), typeof(Tuple<TaleWorlds.CampaignSystem.CharacterDevelopment.TraitObject, int>[]) }));
        var expected = "";
        Server.Call(() =>
        {
            expected = (timeout ? serverQuest.TimeoutQuestLogText : serverQuest.SuccessQuestLogText).ToString();
            if (timeout) serverQuest.CompleteQuestWithTimeOut();
            else
            {
                serverQuest._givenTradeGoods = Amount;
                serverQuest.CompleteQuestWithSuccess();
            }
        });
        environment.FlushCoalescer();
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueJournal>());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        ClientA.Call(() =>
        {
            Assert.True(ownerQuest.IsFinalized);
            Assert.Single(ownerQuest.JournalEntries, log => log.LogText.ToString() == expected);
            var archive = Campaign.Current.LogEntryHistory.FindLastGameActionLog(
                (TaleWorlds.CampaignSystem.LogEntries.JournalLogEntry entry) => entry.IsRelatedTo(ownerQuest));
            Assert.NotNull(archive);
            Assert.True(archive.IsEnded());
            Assert.Single(archive.GetEntries(), log => log.LogText.ToString() == expected);
        });
        ClientB.Call(() => Assert.Null(Campaign.Current.LogEntryHistory.FindLastGameActionLog(
            (TaleWorlds.CampaignSystem.LogEntries.JournalLogEntry entry) => entry.RelatedHero == Get<Hero>(ClientB, scenario.NotableId))));
        Assert.Equal(timeout ? 0 : 1, gold.CountFor(Server));
        Assert.Equal(timeout ? 0 : 1, traits.CountFor(Server));
        foreach (var client in environment.Clients)
        {
            Assert.Equal(0, gold.CountFor(client));
            Assert.Equal(0, traits.CountFor(client));
        }
    }

    private static void OnMapAfterConversation(EnvironmentInstance client, Action act)
    {
        client.Call(() =>
        {
            // Vanilla's menu callback reads the global state manager, which the harness leaves unset.
            var previous = GameStateManager.Current;
            var states = GameStateManager.Current = Game.Current.GameStateManager;
            var map = states.CreateState<MapState>();
            states._gameStates.Add(map);
            try
            {
                act();
            }
            finally
            {
                states._gameStates.Remove(map);
                GameStateManager.Current = previous;
            }
        });
    }

    [Fact]
    public void AuthoritativeSuccessReplay_EndsTheOwnersQuestEvenWithStaleLocalProgress()
    {
        Accept();
        var quest = (Quest)scenario.QuestOf(ClientA);
        ClientA.SimulateMessage(Server.NetPeer, new NetworkIssueFinalized(scenario.NotableId, QuestScenario.IssueId, IssueOutcome.QuestSuccess));
        ClientA.Call(() =>
        {
            Assert.True(quest.IsFinalized);
            Assert.Null(Get<Hero>(ClientA, scenario.NotableId).Issue);
        });
        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
    }

    [Theory]
    [InlineData(false, 150, false)]
    [InlineData(true, 150, false)]
    [InlineData(false, 100, false)]
    [InlineData(false, 150, true)]
    public void CompanionSolution_ChargesOnceAndReturnsTroopsOnlyToItsOwner(bool failure, int companionSkill, bool rejectAfterStart)
    {
        using var nativeState = new NativeHeroStateScope();
        if (rejectAfterStart)
            QuestJournalHistoryAssertions.RegisterJournalBehavior(new[] { Server });
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
                    companion.SetSkillValue(DefaultSkills.Trade, companionSkill);
                    var troop = Get<CharacterObject>(instance, troopId);
                    troop.Level = 20;
                    troop.UpgradeTargets = Array.Empty<CharacterObject>();
                    var issue = (Issue)Get<Hero>(instance, scenario.NotableId).Issue;
                    needed = issue.GetTotalAlternativeSolutionNeededMenCount();
                    funding = issue.RequiredGoldForAlternativeSolution;
                }
            });
        }

        Server.Call(() =>
        {
            var party = Get<MobileParty>(Server, scenario.OwnerPartyId);
            party.MemberRoster.AddToCounts(Get<Hero>(Server, companionId).CharacterObject, 1);
            party.MemberRoster.AddToCounts(Get<CharacterObject>(Server, troopId), needed + 2);
        });
        environment.FlushCoalescer();

        var harmony = new Harmony($"alternative-archive-failure.{Guid.NewGuid():N}");
        if (rejectAfterStart)
            harmony.Patch(AccessTools.Method(typeof(IssueBase), nameof(IssueBase.StartIssueWithAlternativeSolution)),
                postfix: new HarmonyMethod(typeof(ArtisanOverpricedGoodsTests), nameof(ThrowOnServerAccept)));
        try
        {
            ClientA.Call(() =>
            {
                var issue = Get<Hero>(ClientA, scenario.NotableId).Issue;
                var companion = Get<Hero>(ClientA, companionId);
                var troop = Get<CharacterObject>(ClientA, troopId);
                issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                AlternativeSolutionPickerDriver.Commit(ClientA, issue, Get<Hero>(ClientA, scenario.OwnerHeroId),
                    Get<MobileParty>(ClientA, scenario.OwnerPartyId), troop, needed);
                issue.AlternativeSolutionStartConsequence();
                issue.StartIssueWithAlternativeSolution();
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
        environment.FlushCoalescer();
        if (rejectAfterStart)
        {
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAcceptRejected>());
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
            Server.Call(() =>
            {
                var issue = Get<Hero>(Server, scenario.NotableId).Issue;
                Assert.True(issue.IsSolvingWithAlternative);
                Assert.NotEmpty(issue.JournalEntries);
                Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _));
                Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                    entry => entry.IsRelatedTo(issue)));
            });
            return;
        }
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
        foreach (var instance in new[] { Server, ClientA })
            instance.Call(() => Assert.Same(Get<Hero>(instance, companionId),
                Get<Hero>(instance, scenario.NotableId).Issue.AlternativeSolutionHero));
        Server.Call(() => Assert.Equal(10000 - funding, Get<Hero>(Server, scenario.OwnerHeroId).Gold));
        var ownerIssue = ClientA.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
        var observerIssue = ClientB.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
        var initialOwnerJournal = ownerIssue.JournalEntries.Select(log => log.LogText.ToString()).ToHashSet();

        Server.Call(() =>
        {
            var issue = Get<Hero>(Server, scenario.NotableId).Issue;
            issue._failureChance = failure ? 1 : -1;
            issue.CompleteIssueWithAlternativeSolution();
        });
        environment.FlushCoalescer();
        AssertFinished(failure ? 2500 : 2530, 10000 - funding + (failure ? 0 : Payment));
        AssertEffects(failure ? 200 : 210, failure ? 100 : 90, failure ? 0 : 5, failure ? 0 : -10);
        var endingText = ownerIssue.JournalEntries.Last().LogText.ToString();
        Assert.DoesNotContain(endingText, initialOwnerJournal);
        // The observer keeps only the framework's blank placeholder for vanilla's daily tick, never the owner's text.
        Assert.DoesNotContain(observerIssue.JournalEntries, log => log.LogText.ToString() == endingText);
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var party = Get<MobileParty>(instance, scenario.OwnerPartyId);
                var companion = Get<Hero>(instance, companionId);
                var troop = Get<CharacterObject>(instance, troopId);
                Assert.True(needed + 2 == party.MemberRoster.GetTroopCount(troop),
                    $"{instance.GetType().Name} returned roster: {party.MemberRoster.GetTroopCount(troop)}, expected {needed + 2}");
                Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
                Assert.True(companion.HeroState == Hero.CharacterStates.Active,
                    $"{instance.GetType().Name} companion state after return: {companion.HeroState}; messages: {string.Join(",", Server.NetworkSentMessages.GetMessages<NetworkHeroStateChanged>().Where(m => m.HeroId == companionId).Select(m => m.HeroState))}");
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

    private static IQuestTypeDescriptor Descriptor(EnvironmentInstance instance)
    {
        Assert.True(instance.Resolve<IQuestTypeRegistry>().TryGet(typeof(Issue), out var descriptor));
        return descriptor;
    }

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

    private void AssertEffects(float giverPower, float merchantPower, int giverRelation, int merchantRelation)
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                var giver = Get<Hero>(instance, scenario.NotableId);
                var merchant = Get<Hero>(instance, merchantId);
                var owner = Get<Hero>(instance, scenario.OwnerHeroId);
                var observer = Get<Hero>(instance, scenario.OtherHeroId);
                Assert.Equal(giverPower, giver.Power);
                Assert.Equal(merchantPower, merchant.Power);
                Assert.Equal(giverRelation, owner.GetBaseHeroRelation(giver));
                Assert.Equal(merchantRelation, owner.GetBaseHeroRelation(merchant));
                Assert.Equal(0, observer.GetBaseHeroRelation(giver));
                Assert.Equal(0, observer.GetBaseHeroRelation(merchant));
            });
        }
    }

    // The bootstrap stub skips HeroFieldPatches, so this scenario needs the native state transition.
    private sealed class NativeHeroStateScope : IDisposable
    {
        private readonly System.Reflection.MethodInfo target = AccessTools.Method(typeof(Hero), nameof(Hero.ChangeState));
        private readonly Patch stub;
        private readonly Harmony harmony;

        public NativeHeroStateScope()
        {
            stub = Assert.Single(Harmony.GetPatchInfo(target).Prefixes,
                patch => patch.PatchMethod.DeclaringType.FullName == "GameInterface.Tests.Bootstrap.Patches.HeroPatches");
            harmony = new Harmony(stub.owner);
            harmony.Unpatch(target, stub.PatchMethod);
        }

        public void Dispose() => harmony.Patch(target, prefix: new HarmonyMethod(stub.PatchMethod)
        {
            priority = stub.priority,
            before = stub.before,
            after = stub.after,
        });
    }

    private static T Get<T>(EnvironmentInstance instance, string id) where T : class => instance.GetRegisteredObject<T>(id);
}
