using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using GangLeaderQuest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;
using GarrisonQuest = TaleWorlds.CampaignSystem.Issues.LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest;
using SmugglersQuest = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssueQuest;

namespace E2E.Tests.Services.Issues;

/// <summary>Exercises the shared owner-change dispatcher relay and its owner-only ending.</summary>
public class SettlementOwnerChangeRelayTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly string settlementId;
    private readonly Dictionary<EnvironmentInstance, OwnerChangeQuest> quests = new();

    public SettlementOwnerChangeRelayTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
        settlementId = environment.CreateRegisteredObject<Settlement>();

        foreach (var instance in new[] { scenario.Server, scenario.ClientA, scenario.ClientB })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

                using (new AllowedThread())
                {
                    owner._partyBelongedTo = party;
                    giver.Issue._issueState = TaleWorlds.CampaignSystem.Issues.IssueBase.IssueState.SolvingWithQuestSolution;
                    if (instance != scenario.ClientB)
                    {
                        var quest = new OwnerChangeQuest(giver, settlement);
                        giver.Issue.IssueQuest = quest;
                        quests.Add(instance, quest);
                        if (instance == scenario.ClientA)
                        {
                            Game.Current.PlayerTroop = owner.CharacterObject;
                            Campaign.Current.MainParty = party;
                            quest.StartQuest();
                        }
                    }
                }

                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(
                    scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA));
            });
        }
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void TheServerRelaysTheEventAsTheOwnerAndEndsOnlyTheirQuest()
    {
        Dispatch(scenario.Server, settlementId);
        environment.FlushCoalescer();

        var finalized = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestCancel, finalized.Outcome);
        Assert.Equal(QuestScenario.IssueId, finalized.IssueId);
        Assert.Equal(scenario.NotableId, finalized.IssueOwnerId);
        Assert.Empty(scenario.ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());

        scenario.Server.Call(() =>
        {
            var call = Assert.Single(quests[scenario.Server].Calls);
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.Same(owner, call.MainHero);
            Assert.Same(party, call.MainParty);
            Assert.Same(settlement, call.Settlement);
            Assert.True(call.OpenToClaim);
            Assert.Same(owner, call.NewOwner);
            Assert.Same(giver, call.OldOwner);
            Assert.Same(owner, call.Capturer);
            Assert.Equal(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege, call.Detail);
            Assert.False(scenario.Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(
                scenario.NotableId, QuestScenario.IssueId, out _));
        });

        foreach (var instance in new[] { scenario.Server, scenario.ClientA, scenario.ClientB })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
                Assert.Null(giver.Issue);
            });
        }

        scenario.ClientA.Call(() =>
        {
            Assert.True(quests[scenario.ClientA].IsFinalized);
            Assert.DoesNotContain(quests[scenario.ClientA], Campaign.Current.QuestManager.Quests);
        });
        scenario.ClientB.Call(() => Assert.Empty(Campaign.Current.QuestManager.Quests));
    }

    [Fact]
    public void AnUnrelatedSettlementDoesNotEndTheQuest()
    {
        Dispatch(scenario.Server, environment.CreateRegisteredObject<Settlement>());

        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        scenario.Server.Call(() => Assert.Single(quests[scenario.Server].Calls));
        scenario.ClientA.Call(() => Assert.True(quests[scenario.ClientA].IsOngoing));
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
    }

    [Fact]
    public void AClientOwnershipReplayDoesNotRequestOrDecideTheEnding()
    {
        scenario.ClientA.Call(() =>
        {
            using (new AllowedThread())
            {
                Dispatch(scenario.ClientA, settlementId);
            }
        });

        Assert.Empty(scenario.ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Empty(quests[scenario.Server].Calls);
        scenario.ClientA.Call(() => Assert.True(quests[scenario.ClientA].IsOngoing));
    }

    [Theory]
    [InlineData(typeof(GangLeaderQuest))]
    [InlineData(typeof(GarrisonQuest))]
    [InlineData(typeof(SmugglersQuest))]
    public void ExistingNativeListenersAreReachedOnce(Type questType)
    {
        using var calls = new MethodCallRecorder(AccessTools.Method(questType, "OnSettlementOwnerChanged"));
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            var quest = (QuestBase)RuntimeHelpers.GetUninitializedObject(questType);
            quest.QuestGiver = giver;
            quest._questState = QuestBase.QuestStates.Ongoing;
            giver.Issue.IssueQuest = quest;
        });

        Dispatch(scenario.Server, settlementId);

        Assert.Equal(1, calls.CountFor(scenario.Server));
        Assert.Equal(0, calls.CountFor(scenario.ClientA));
        Assert.Equal(0, calls.CountFor(scenario.ClientB));
    }

    private void Dispatch(EnvironmentInstance instance, string changedSettlementId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(changedSettlementId, out var settlement));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            CampaignEventDispatcher.Instance.OnSettlementOwnerChanged(settlement, true, owner, giver, owner,
                ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege);
        });
    }

    private sealed class OwnerChangeQuest : QuestBase
    {
        private readonly Settlement target;
        public readonly List<(Settlement Settlement, bool OpenToClaim, Hero NewOwner, Hero OldOwner, Hero Capturer,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail Detail, Hero MainHero, MobileParty MainParty)> Calls = new();

        public OwnerChangeQuest(Hero giver, Settlement target)
            : base("owner_change_quest", giver, CampaignTime.Never, 0)
        {
            this.target = target;
        }

        public override TextObject Title => new TextObject("Owner change quest");
        public override bool IsRemainingTimeHidden => true;
        public override void SetDialogs() { }
        public override void InitializeQuestOnGameLoad() { }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        }

        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner,
            Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            Calls.Add((settlement, openToClaim, newOwner, oldOwner, capturerHero, detail, Hero.MainHero, MobileParty.MainParty));
            if (settlement == target && newOwner == Hero.MainHero)
                CompleteQuestWithCancel();
        }
    }
}
