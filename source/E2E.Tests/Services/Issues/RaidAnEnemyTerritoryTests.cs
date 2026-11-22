using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace E2E.Tests.Services.Issues;

public sealed class RaidAnEnemyTerritoryTests : IDisposable
{
    private static readonly MethodBase[] DialogAndTextHelpers =
    {
        AccessTools.Method(typeof(Quest), nameof(Quest.SetDialogs)),
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.SetCharacterProperties)),
    };

    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly string enemyId;
    private readonly string[] villages;

    private EnvironmentInstance Server => scenario.Server;
    private EnvironmentInstance Owner => scenario.ClientA;
    private EnvironmentInstance Bystander => scenario.ClientB;

    public RaidAnEnemyTerritoryTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        var friendlyId = environment.CreateRegisteredObject<Kingdom>();
        enemyId = environment.CreateRegisteredObject<Kingdom>();
        villages = Enumerable.Range(0, 3).Select(_ => environment.CreateRegisteredObject<Settlement>()).ToArray();
        scenario = new QuestScenario(environment, (instance, giver) =>
        {
            var friendly = instance.GetRegisteredObject<Kingdom>(friendlyId);
            var enemy = instance.GetRegisteredObject<Kingdom>(enemyId);
            friendly.Id = new MBGUID(201);
            enemy.Id = new MBGUID(202);
            giver.Clan._kingdom = friendly;
            VillageHostileFactionStanceHelper.ApplyWarStance(friendly, enemy);
            var issue = new Issue(giver);
            AccessTools.Field(typeof(Issue), nameof(Issue._enemyKingdom)).SetValue(issue, enemy);
            return issue;
        });

        foreach (var instance in new[] { Server, Owner, Bystander })
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    var owner = instance.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
                    var bystander = instance.GetRegisteredObject<Hero>(scenario.OtherHeroId);
                    owner.Clan._kingdom = Giver(instance).Clan.Kingdom;
                    owner.Gold = bystander.Gold = 100;
                }
                new IssuesCampaignBehavior().RegisterEvents();
            });
        }
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void AcceptanceUsesTheServersDeadlineWhenTheClientClockDiffers()
    {
        long serverDeadline = 0;
        Server.Call(() => serverDeadline = CampaignTime.DaysFromNow(60).NumTicks);
        Owner.Call(() =>
        {
            Campaign.Current.MapTimeTracker._numTicks += CampaignTime.Days(1).NumTicks;
            Assert.NotEqual(serverDeadline, CampaignTime.DaysFromNow(60).NumTicks);
        });

        var accepted = Accept();
        Assert.True(accepted.HasQuestDueTime);
        Assert.Equal(serverDeadline, accepted.QuestDueTimeTicks);
        Owner.Call(() => Assert.Equal(serverDeadline, QuestOn(Owner).QuestDueTime.NumTicks));
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(2, false)]
    public void OwnerTurnInPaysOnceOnlyWhenTheServerHasThreeRaids(int serverRaids, bool pays)
    {
        Accept();
        GiveRaidCredit(Server, serverRaids);
        GiveRaidCredit(Owner, 3);
        var before = ServerRewards();

        // The client has the dialogue option in both cases. The server decides using its own progress.
        var ownerQuest = QuestOn(Owner);
        Owner.Call(() => ownerQuest.MainHeroRaidedAllVillages(), DialogAndTextHelpers);
        Assert.Single(Owner.NetworkSentMessages.GetMessages<RequestQuestBranch>());

        if (pays)
        {
            AssertOutcome(IssueOutcome.QuestSuccess);
            Assert.True(ownerQuest.IsFinalized);
        }
        else
        {
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
            Server.Call(() => Assert.True(QuestOn(Server).IsOngoing));
            Assert.True(ownerQuest.IsOngoing);
        }

        AssertRewards(before, pays);
    }

    [Theory]
    [InlineData(3, IssueOutcome.QuestSuccess)]
    [InlineData(2, IssueOutcome.QuestCancel)]
    public void GiverDeathPaysForThreeRaidsBeforeTheGenericCancellation(int raids, IssueOutcome outcome)
    {
        Accept();
        GiveRaidCredit(Server, raids);
        var before = ServerRewards();

        Server.Call(() => Campaign.Current.IssueManager.OnHeroKilled(Giver(Server), null,
            KillCharacterAction.KillCharacterActionDetail.DiedInBattle, false), DialogAndTextHelpers);

        AssertOutcome(outcome);
        AssertRewards(before, outcome == IssueOutcome.QuestSuccess);
    }

    [Theory]
    [InlineData(3, IssueOutcome.QuestSuccess)]
    [InlineData(0, IssueOutcome.QuestTimeOut)]
    public void DailyTickAfterTheDeadlineEndsTheUnstartedServerQuestOnce(int raids, IssueOutcome outcome)
    {
        Accept();
        GiveRaidCredit(Server, raids);
        var before = ServerRewards();
        Server.Call(() =>
        {
            var quest = QuestOn(Server);
            Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
            quest.ChangeQuestDueTime(CampaignTime.Now - CampaignTime.Days(1));
            Assert.True(quest.QuestDueTime.IsPast);
            CampaignEventDispatcher.Instance.DailyTick();
        }, DialogAndTextHelpers);

        AssertOutcome(outcome);
        AssertRewards(before, outcome == IssueOutcome.QuestSuccess);
    }

    [Fact]
    public void TheBystanderJournalHidesAnOngoingRaidQuestLoadedWithTheCampaign()
    {
        Accept();
        Bystander.Call(() =>
        {
            Assert.Null(Giver(Bystander).Issue.IssueQuest);
            var mirror = new Quest("loaded_raid_quest", Giver(Bystander), CampaignTime.DaysFromNow(60),
                Bystander.GetRegisteredObject<Kingdom>(enemyId));
            using (new AllowedThread())
            {
                Giver(Bystander).Issue.IssueQuest = mirror;
                Campaign.Current.QuestManager.OnQuestStarted(mirror);
            }
            Assert.True(mirror.IsOngoing);
            Assert.Contains(mirror, Campaign.Current.QuestManager.Quests);
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                new CampaignBehaviorBase[] { new ViewDataTrackerCampaignBehavior() }));

            var previousTextManager = GameTexts._gameTextManager;
            try
            {
                GameTexts._gameTextManager = new GameTextManager();
                var journal = new QuestsVM(() => { });
                Assert.DoesNotContain(journal.ActiveQuestsList, row => row.Quest == mirror);
                Assert.True(mirror.IsOngoing);
                Assert.Contains(mirror, Campaign.Current.QuestManager.Quests);
            }
            finally
            {
                GameTexts._gameTextManager = previousTextManager;
            }
        }, DialogAndTextHelpers);
    }

    private NetworkQuestSolutionAccepted Accept()
    {
        Owner.Call(() =>
        {
            var hero = Owner.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
            using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
            {
                Giver(Owner).Issue.StartIssueWithQuest();
                QuestOn(Owner).QuestAcceptedByPlayerConsequences();
            }
        }, DialogAndTextHelpers);

        return Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
    }

    private void GiveRaidCredit(EnvironmentInstance instance, int count) => instance.Call(() =>
    {
        var quest = QuestOn(instance);
        quest._raidedVillages.AddRange(villages.Take(count).Select(instance.GetRegisteredObject<Settlement>));
        quest._raidedVillagesTrackLog.UpdateCurrentProgress(count);
    });

    private void AssertOutcome(IssueOutcome expected)
    {
        var ended = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(QuestScenario.IssueId, ended.IssueId);
        Assert.Equal(expected, ended.Outcome);
        foreach (var instance in new[] { Server, Owner, Bystander })
            instance.Call(() => Assert.Null(Giver(instance).Issue));
    }

    private (Rewards Owner, Rewards Bystander) ServerRewards()
    {
        (Rewards, Rewards) rewards = default;
        Server.Call(() => rewards = (ReadRewards(scenario.OwnerHeroId), ReadRewards(scenario.OtherHeroId)));
        return rewards;
    }

    private Rewards ReadRewards(string heroId)
    {
        var hero = Server.GetRegisteredObject<Hero>(heroId);
        return new Rewards(hero.Gold, hero.Clan.Renown, hero.Clan.Influence);
    }

    private void AssertRewards((Rewards Owner, Rewards Bystander) before, bool pays)
    {
        var after = ServerRewards();
        var owner = before.Owner;
        Assert.Equal(pays ? new Rewards(owner.Gold + 15000, owner.Renown + 5, owner.Influence + 20) : owner,
            after.Owner);
        Assert.Equal(before.Bystander, after.Bystander);
    }

    private Hero Giver(EnvironmentInstance instance) => instance.GetRegisteredObject<Hero>(scenario.NotableId);

    private Quest QuestOn(EnvironmentInstance instance) => Assert.IsType<Quest>(Giver(instance).Issue.IssueQuest);

    private readonly record struct Rewards(int Gold, float Renown, float Influence);
}
