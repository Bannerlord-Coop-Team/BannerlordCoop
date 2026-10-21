using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit.Abstractions;
using System.Runtime.CompilerServices;
using E2E.Tests.Environment.Instance;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using SandBox.GauntletUI.Map;
using GameInterface.Services.MapEvents.Messages.Conversation;
using Coop.Core.Client.Services.MobileParties.Messages;

namespace E2E.Tests.Services.Issues;

public class QuestRequestOwnershipTests : IDisposable
{
    private const byte Branch = (byte)GangLeaderNeedsToOffloadStolenGoodsBranch.SuccessByKeepingGoods;

    private readonly StubIssueQuestGenerator quests = new();
    private readonly StubIssuePreconditions preconditions = new(QuestScenario.IssueType);
    private readonly MethodCallRecorder consequences;
    private readonly QuestScenario scenario;

    private E2ETestEnvironment TestEnvironment { get; }

    public QuestRequestOwnershipTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(TestEnvironment);
        consequences = new MethodCallRecorder(Priority.Last,
            AccessTools.Method(typeof(Quest), nameof(Quest.SucceedQuestByPayingAndKeepingTheGoods)),
            AccessTools.Method(typeof(Quest), nameof(Quest.SucceedQuestByPayingAndGivingTheGoodsBack)));
    }

    public void Dispose()
    {
        consequences.Dispose();
        TestEnvironment.Dispose();
        quests.Dispose();
        preconditions.Dispose();
    }

    [Fact]
    public void AnOutcomeRequestFromTheOwnerEndsTheIssueEverywhere()
    {
        scenario.Accept(scenario.ClientA);
        var quest = scenario.QuestOf(scenario.ClientA);

        scenario.ClientA.Call(() => scenario.ClientA.Resolve<IMessageBroker>().Publish(this, new QuestOutcomeRequested(quest, IssueOutcome.QuestCancel)));

        var finalized = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestCancel, finalized.Outcome);

        foreach (var instance in new[] { scenario.Server, scenario.ClientA, scenario.ClientB })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
                Assert.Null(notable.Issue);
            });
        }
    }

    [Fact]
    public void AnOutcomeRequestFromAPlayerWhoDoesNotOwnTheIssueIsIgnored()
    {
        scenario.Accept(scenario.ClientA);
        var mirrorQuest = scenario.QuestOf(scenario.Server);
        var foreignQuest = scenario.AttachQuest(scenario.ClientB);

        scenario.ClientB.Call(() => scenario.ClientB.Resolve<IMessageBroker>().Publish(this, new QuestOutcomeRequested(foreignQuest, IssueOutcome.QuestCancel)));

        Assert.Single(scenario.ClientB.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());

        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.NotNull(notable.Issue);
            Assert.True(notable.Issue.IsSolvingWithQuest);
            Assert.True(mirrorQuest.IsOngoing);
        });
    }

    [Fact]
    public void AnOutcomeRequestForAnOwnedIssueThatHasNoQuestChangesNothing()
    {
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.True(scenario.Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA));

            using (new AllowedThread())
            {
                notable.Issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
            }
        });
        var attachedQuest = scenario.AttachQuest(scenario.ClientA);

        scenario.ClientA.Call(() => scenario.ClientA.Resolve<IMessageBroker>().Publish(this, new QuestOutcomeRequested(attachedQuest, IssueOutcome.QuestCancel)));

        Assert.Single(scenario.ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());

        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var notable));
            Assert.NotNull(notable.Issue);
            Assert.True(notable.Issue.IsSolvingWithAlternative);
        });
    }

    [Fact]
    public void ABranchRequestForARealQuestBeforeGoodsAcquisitionIsRejected()
    {
        scenario.Accept(scenario.ClientA);
        var quest = AttachRealQuest(scenario.ClientA);
        AttachRealQuest(scenario.Server);

        scenario.ClientA.Call(() =>
        {
            scenario.ClientA.Resolve<IMessageBroker>().Publish(this, new QuestBranchRequested(quest, Branch));
        });

        Assert.Single(scenario.ClientA.NetworkSentMessages.GetMessages<RequestQuestBranch>());
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(0, consequences.CountFor(scenario.Server));
    }

    private Quest AttachRealQuest(EnvironmentInstance instance)
    {
        Quest quest = null!;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            quest = (Quest)RuntimeHelpers.GetUninitializedObject(typeof(Quest));
            quest.QuestGiver = giver;
            using (new AllowedThread()) giver.Issue.IssueQuest = quest;
        });
        return quest;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ServerValidatedPurchase_AllowsOneRealQuestBranchAndChargesOnce(bool giveBack, bool ended)
    {
        scenario.Accept(scenario.ClientA);
        var clientQuest = AttachRealQuest(scenario.ClientA);
        var serverQuest = AttachRealQuest(scenario.Server);
        var hideoutId = TestEnvironment.CreateRegisteredObject<Settlement>();
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Settlement>(hideoutId, out var hideout));
            AccessTools.Field(typeof(Quest), nameof(Quest._questHideout)).SetValue(serverQuest, hideout);
            AccessTools.Field(typeof(Quest), nameof(Quest._stolenTradeGoodPrice)).SetValue(serverQuest, 100);
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var hero));
            using (new AllowedThread())
            {
                party.CurrentSettlement = hideout;
                hero.ChangeHeroGold(1000 - hero.Gold);
            }
        });

        scenario.ClientA.Call(() => scenario.ClientA.Resolve<IGangLeaderGoodsService>().Purchase(clientQuest));
        scenario.ClientA.Call(() => scenario.ClientA.Resolve<IGangLeaderGoodsService>().Purchase(clientQuest));
        scenario.Server.Call(() =>
        {
            Assert.True(serverQuest._isPayingForGoods);
            Assert.True(serverQuest._playerHasTheGoods);
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var hero));
            Assert.Equal(900, hero.Gold);
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Settlement>(hideoutId, out var hideout));
            using (new MainHeroSubstitutionScope(hero, party))
                scenario.Server.Resolve<IGangLeaderGoodsService>().LeaveHideout(serverQuest, party, hideout);
        });

        // The accepted exit finishes the outgoing encounter after the merchant was authorized.
        scenario.ClientA.Call(() =>
        {
            Assert.True(scenario.ClientA.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
            Assert.True(scenario.ClientA.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var hero));
            using (new MainHeroSubstitutionScope(hero, party))
                scenario.ClientA.Resolve<IMessageBroker>().Publish(this,
                    new NetworkSettlementEncounterLeaveResult(0, SettlementEncounterLeaveOutcome.Applied));
        });
        Assert.Contains(scenario.ClientA.NetworkSentMessages.GetMessages<NetworkConversationEnded>(), message => message.RequestId == null);

        if (ended)
        {
            scenario.ClientA.Call(() =>
            {
                Assert.True(scenario.ClientA.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var merchant));
                AccessTools.Field(typeof(Quest), nameof(Quest._counterOfferHero))
                    .SetValue(clientQuest, merchant);
                CampaignEventDispatcher.Instance.OnConversationEnded(new List<CharacterObject>
                {
                    clientQuest._counterOfferHero.CharacterObject,
                });
            });
            Assert.Single(scenario.ClientA.NetworkSentMessages.GetMessages<RequestGangLeaderMerchantConversationEnded>());
        }

        scenario.ClientA.Call(() =>
        {
            if (giveBack) clientQuest.SucceedQuestByPayingAndGivingTheGoodsBack();
            else clientQuest.SucceedQuestByPayingAndKeepingTheGoods();
        });
        scenario.ClientA.Call(() =>
        {
            if (giveBack) clientQuest.SucceedQuestByPayingAndGivingTheGoodsBack();
            else clientQuest.SucceedQuestByPayingAndKeepingTheGoods();
        });
        Assert.Equal(ended ? 0 : 1, consequences.CountFor(scenario.Server));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false, true)]
    public void ServerBattleResult_AuthorizesGoodsOrFailsOnce(bool won, bool giveBack, bool paid = false)
    {
        scenario.Accept(scenario.ClientA);
        var clientQuest = AttachRealQuest(scenario.ClientA);
        var serverQuest = AttachRealQuest(scenario.Server);
        if (paid)
        {
            scenario.ClientA.Call(() =>
            {
                clientQuest._isPayingForGoods = true;
                clientQuest._playerHasTheGoods = true;
            });
            scenario.Server.Call(() =>
            {
                serverQuest._isPayingForGoods = true;
                serverQuest._playerHasTheGoods = true;
            });
        }
        using var failures = new MethodCallRecorder(Priority.Last,
            AccessTools.Method(typeof(Quest), nameof(Quest.FailQuestByKeepingTheGoods)),
            AccessTools.Method(typeof(Quest), nameof(Quest.FailQuestByGivingBackTheGoods)),
            AccessTools.Method(typeof(Quest), nameof(Quest.FailQuestByLosingHideoutBattle)));
        var hideoutId = TestEnvironment.CreateRegisteredObject<Settlement>();
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Settlement>(hideoutId, out var hideout));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var ownerParty));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var hero));
            AccessTools.Field(typeof(Quest), nameof(Quest._questHideout)).SetValue(serverQuest, hideout);
            var enemy = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var battle = GameObjectCreator.CreateInitializedObject<MapEvent>();
            using (new AllowedThread())
            {
                hero.PartyBelongedTo = ownerParty;
                battle.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
                battle.Initialize(ownerParty.Party, enemy.Party, new FieldBattleEventComponent(battle), MapEvent.BattleTypes.Hideout);
                battle.MapEventVisual = null;
                battle.MapEventSettlement = hideout;
                battle.BattleState = won ? BattleState.AttackerVictory : BattleState.DefenderVictory;
            }
            var service = scenario.Server.Resolve<IGangLeaderGoodsService>();
            using (new MainHeroSubstitutionScope(hero, ownerParty))
            {
                service.ObserveBattle(serverQuest, battle);
                // Defeat can remove the player's party before the completion callback.
                if (!won) hero.PartyBelongedTo = null;
                service.BattleEnded(serverQuest, battle);
                service.BattleEnded(serverQuest, battle);
                if (won) service.LeaveHideout(serverQuest, ownerParty, hideout);
            }
            Assert.Equal(won, serverQuest._playerHasTheGoods);
        }, new[] { AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)) });

        if (won)
        {
            scenario.ClientA.Call(() =>
            {
                Assert.True(clientQuest._playerHasTheGoods);
                Assert.Equal(paid, clientQuest._isPayingForGoods);
                if (!paid) Assert.True(clientQuest._isFightingForGoods);
            });
            scenario.ClientA.Call(() =>
            {
                if (paid) clientQuest.SucceedQuestByPayingAndKeepingTheGoods();
                else if (giveBack) clientQuest.FailQuestByGivingBackTheGoods();
                else clientQuest.FailQuestByKeepingTheGoods();
            });
            scenario.ClientA.Call(() =>
            {
                if (paid) clientQuest.SucceedQuestByPayingAndKeepingTheGoods();
                else if (giveBack) clientQuest.FailQuestByGivingBackTheGoods();
                else clientQuest.FailQuestByKeepingTheGoods();
            });
        }
        Assert.Equal(paid ? 0 : 1, failures.CountFor(scenario.Server));
        if (paid) Assert.Equal(1, consequences.CountFor(scenario.Server));
    }

    [Fact]
    public void ABranchRequestBeforeTheClientRecordsTheVanillaStateIsIgnored()
    {
        scenario.Accept(scenario.ClientA);
        var quest = scenario.QuestOf(scenario.ClientA);

        scenario.ClientA.Call(() =>
            scenario.ClientA.Resolve<IMessageBroker>().Publish(this, new QuestBranchRequested(quest, Branch)));

        Assert.Equal(0, consequences.CountFor(scenario.Server));
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }

    [Fact]
    public void ABranchRequestFromAPlayerWhoDoesNotOwnTheIssueIsIgnored()
    {
        scenario.Accept(scenario.ClientA);
        var foreignQuest = scenario.AttachQuest(scenario.ClientB);

        scenario.ClientB.Call(() => scenario.ClientB.Resolve<IMessageBroker>().Publish(this, new QuestBranchRequested(foreignQuest, Branch)));

        Assert.Single(scenario.ClientB.NetworkSentMessages.GetMessages<RequestQuestBranch>());
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
    }
}
