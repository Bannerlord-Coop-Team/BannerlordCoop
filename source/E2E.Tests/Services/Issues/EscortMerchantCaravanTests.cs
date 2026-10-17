using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.EscortMerchantCaravan;
using GameInterface.Services.Players;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest;

namespace E2E.Tests.Services.Issues;

public class EscortMerchantCaravanTests : IDisposable
{
    private static readonly MethodBase[] WithoutWorldSetup =
    {
        AccessTools.Method(typeof(Quest), nameof(Quest.SpawnCaravan)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.PlayerStartsQuestLogText)),
    };

    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly MethodCallRecorder spawn;
    private readonly string caravanId;

    private EnvironmentInstance Server => scenario.Server;
    private EnvironmentInstance Owner => scenario.ClientA;
    private EnvironmentInstance Other => scenario.ClientB;

    public EscortMerchantCaravanTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        spawn = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.SpawnCaravan)));
        scenario = new QuestScenario(environment, hero => new Issue(hero));
        caravanId = environment.CreateRegisteredObject<MobileParty>();

        var townId = environment.CreateRegisteredObject<Settlement>();
        var townComponentId = environment.CreateRegisteredObject<Town>();
        var clanId = environment.CreateRegisteredObject<Clan>();
        foreach (var instance in new[] { Server, Owner, Other })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(townId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townComponentId, out var townComponent));
                using (new AllowedThread())
                {
                    giver.StayingInSettlement = town;
                    giver.Occupation = Occupation.Merchant;
                    giver.Clan = clan;
                    town.Town = townComponent;
                    town.Town.Security = 50;
                }
            });
        }
    }

    public void Dispose()
    {
        spawn.Dispose();
        environment.Dispose();
    }

    private void Accept()
    {
        Owner.Call(() =>
        {
            Assert.True(Owner.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            giver.Issue.StartIssueWithQuest();
            ((Quest)giver.Issue.IssueQuest).QuestAcceptedConsequences();
        }, WithoutWorldSetup);
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
    }

    private Quest QuestOn(EnvironmentInstance instance) => Assert.IsType<Quest>(scenario.QuestOf(instance));

    [Fact]
    public void RealAcceptKeepsTheQuestOnItsOwnerAndSpawnsOnlyOnTheServer()
    {
        Accept();
        Assert.Equal(1, spawn.CountFor(Server));
        Assert.Equal(0, spawn.CountFor(Owner));
        Assert.Equal(0, spawn.CountFor(Other));
        Owner.Call(() => Assert.Contains(QuestOn(Owner), Campaign.Current.QuestManager.Quests));
        Server.Call(() => Assert.DoesNotContain(QuestOn(Server), Campaign.Current.QuestManager.Quests));
        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            Assert.Null(giver.Issue.IssueQuest);
        });
    }

    [Fact]
    public void TheServerPublishesCaravanAndVisitedSettlementProgressOnlyToTheOwner()
    {
        Accept();
        Server.Call(() =>
        {
            var quest = QuestOn(Server);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(caravanId, out var caravan));
            quest._questCaravanMobileParty = caravan;
            quest._daysSpentForEscorting = 4;
            quest._visitedSettlements.Add(quest.QuestGiver.CurrentSettlement);
            quest._playerStartsQuestLog = quest.AddDiscreteLog(new TextObject("Escort started"), new TextObject("Visited Settlements"), 1, 6);
            Server.Resolve<IEscortProgress>().Publish(quest);
        });
        Owner.Call(() =>
        {
            var quest = QuestOn(Owner);
            Assert.True(Owner.ObjectManager.TryGetObject<MobileParty>(caravanId, out var caravan));
            Assert.Same(caravan, quest._questCaravanMobileParty);
            Assert.Equal(4, quest._daysSpentForEscorting);
            Assert.Single(quest._visitedSettlements);
            Assert.Equal(1, quest._playerStartsQuestLog.CurrentProgress);
        });
        Other.Call(() =>
        {
            Assert.True(Other.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            Assert.Null(giver.Issue.IssueQuest);
        });
    }

    [Fact]
    public void AProgressMessageForAnotherIssueCannotReplaceTheOwnersCaravan()
    {
        Accept();
        Owner.SimulateMessage(Server.NetPeer, new NetworkEscortProgress
        {
            GiverId = scenario.NotableId, IssueId = "old_issue", ControllerId = QuestScenario.ControllerA,
            CaravanId = caravanId,
        });
        Owner.Call(() => Assert.Null(QuestOn(Owner)._questCaravanMobileParty));
    }

    [Fact]
    public void ClientTicksCannotAdvanceOrFinishTheAuthoritativeEscort()
    {
        Accept();
        Owner.Call(() =>
        {
            var quest = QuestOn(Owner);
            var days = quest._daysSpentForEscorting;
            quest.DailyTick();
            quest.HourlyTick();
            quest.SuccessConsequences(false);
            quest.FailConsequences();
            quest.FailByPlayerHostileConsequences();
            Assert.Equal(days, quest._daysSpentForEscorting);
            Assert.True(quest.IsOngoing);
        });
        Assert.Empty(Owner.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessPaysOnlyTheOwnerForTheAuthoritativeDaysAndUpdatesTheTown(bool noTarget)
    {
        Accept();
        var ownerQuest = QuestOn(Owner);
        Server.Call(() =>
        {
            var quest = QuestOn(Server);
            var players = Server.Resolve<IPlayerManager>();
            Assert.True(players.TryGetPlayer(QuestScenario.ControllerA, out var owner));
            Assert.True(players.TryGetPlayer(QuestScenario.ControllerB, out var other));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(owner.HeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(other.HeroId, out var otherHero));
            var beforeGold = ownerHero.Gold;
            var otherGold = otherHero.Gold;
            var prosperity = quest.QuestGiver.CurrentSettlement.Town.Prosperity;
            quest._daysSpentForEscorting = 4;
            Server.Resolve<IOutcomeDispatcher>().RunAsOwners<Quest>(q => q.SuccessConsequences(noTarget));
            Assert.Equal(beforeGold + Math.Min(8000, quest.RewardGold * 4), ownerHero.Gold);
            Assert.Equal(otherGold, otherHero.Gold);
            Assert.Equal(prosperity + 10, quest.QuestGiver.CurrentSettlement.Town.Prosperity);
            Assert.True(quest.IsFinalized);
        });
        Owner.Call(() => Assert.True(ownerQuest.IsFinalized));
    }

    [Theory]
    [InlineData(false, false, -10)]
    [InlineData(true, false, -10)]
    [InlineData(false, true, -20)]
    public void FailureRunsItsOwnTownConsequenceOnce(bool banditsWon, bool hostile, int prosperityChange)
    {
        Accept();
        var ownerQuest = QuestOn(Owner);
        Server.Call(() =>
        {
            var quest = QuestOn(Server);
            var town = quest.QuestGiver.CurrentSettlement.Town;
            var prosperity = town.Prosperity;
            Server.Resolve<IOutcomeDispatcher>().RunAsOwners<Quest>(q =>
            {
                if (hostile) q.FailByPlayerHostileConsequences();
                else q.FailConsequences(banditsWon);
            });
            Assert.Equal(prosperity + prosperityChange, town.Prosperity);
            Assert.True(quest.IsFinalized);
        });
        Owner.Call(() => Assert.True(ownerQuest.IsFinalized));
    }

    [Theory]
    [InlineData(IssueOutcome.QuestCancel)]
    [InlineData(IssueOutcome.QuestFail)]
    [InlineData(IssueOutcome.QuestSuccess)]
    [InlineData(IssueOutcome.QuestTimeOut)]
    public void FinalizationReplaysWithoutRunningWorldCleanupOnTheOwner(IssueOutcome outcome)
    {
        Accept();
        var quest = QuestOn(Owner);
        using var cleanup = new MethodCallRecorder(Priority.Last, AccessTools.Method(typeof(Quest), nameof(Quest.OnFinalize)));
        Server.Call(() =>
        {
            var serverQuest = QuestOn(Server);
            serverQuest.AddLog(new TextObject("Final escort result"));
            if (outcome == IssueOutcome.QuestCancel) serverQuest.CompleteQuestWithCancel();
            else if (outcome == IssueOutcome.QuestFail) serverQuest.CompleteQuestWithFail();
            else if (outcome == IssueOutcome.QuestSuccess) serverQuest.CompleteQuestWithSuccess();
            else serverQuest.CompleteQuestWithTimeOut();
        }, new[] { AccessTools.Method(typeof(Quest), nameof(Quest.OnTimedOut)) });
        Assert.Equal(1, cleanup.CountFor(Server));
        Assert.Equal(0, cleanup.CountFor(Owner));
        Owner.Call(() =>
        {
            Assert.True(quest.IsFinalized);
            Assert.Contains(quest.JournalEntries, log => log.LogText.ToString() == "Final escort result");
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
    }
}
