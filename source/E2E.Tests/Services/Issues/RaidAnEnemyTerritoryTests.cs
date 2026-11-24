using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.CreationCapture;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace E2E.Tests.Services.Issues;

public sealed class RaidAnEnemyTerritoryTests : IDisposable
{
    private const int RewardGold = 15000;
    private const int RenownBonus = 5;
    private const int InfluenceBonus = 20;

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
    public void CreationUsesTheServersEnemyKingdomOnBothClients()
    {
        var giverId = environment.CreateRegisteredObject<Hero>();
        var clientEnemyId = environment.CreateRegisteredObject<Kingdom>();
        var instances = new[] { Server, Owner, Bystander };
        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                var giver = instance.GetRegisteredObject<Hero>(giverId);
                var localEnemy = instance.GetRegisteredObject<Kingdom>(instance == Server ? enemyId : clientEnemyId);
                using (new AllowedThread())
                {
                    giver.Clan = Giver(instance).Clan;
                    if (instance != Server) localEnemy.Id = new MBGUID(203);
                    VillageHostileFactionStanceHelper.ApplyWarStance(giver.MapFaction, localEnemy);
                    // Give each constructor one local enemy so the client roll must be replaced.
                    var kingdoms = Campaign.Current.CampaignObjectManager._kingdoms;
                    kingdoms.Clear();
                    kingdoms.Add(localEnemy);
                }

                Assert.Same(localEnemy, Assert.Single(Kingdom.All, kingdom => kingdom.IsAtWarWith(giver.MapFaction)));
            });
        }

        Server.Call(() =>
        {
            var giver = Server.GetRegisteredObject<Hero>(giverId);
            var potential = new PotentialIssueData((in PotentialIssueData _, Hero owner) =>
                new Issue(owner), typeof(Issue), IssueBase.IssueFrequency.VeryCommon);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());
        Assert.False(string.IsNullOrEmpty(created.IssueId));
        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                var issue = Assert.IsType<Issue>(instance.GetRegisteredObject<Hero>(giverId).Issue);
                Assert.Equal(created.IssueId, issue.StringId);
                Assert.Same(instance.GetRegisteredObject<Kingdom>(enemyId), issue._enemyKingdom);
            });
        }
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

        Server.Call(() =>
        {
            var quest = QuestOn(Server);
            Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
            var completed = new List<QuestBase>();
            CampaignEvents.OnQuestCompletedEvent.AddNonSerializedListener(this, (ended, _) => completed.Add(ended));

            CampaignEventDispatcher.Instance.OnHeroKilled(Giver(Server), null,
                KillCharacterAction.KillCharacterActionDetail.DiedInBattle, false);

            Assert.Same(quest, Assert.Single(completed));
            Assert.True(quest.IsFinalized);
        }, DialogAndTextHelpers);

        AssertOutcome(outcome);
        AssertRewards(before, outcome == IssueOutcome.QuestSuccess);
    }

    [Theory]
    [InlineData(2, IssueOutcome.QuestCancel)]
    [InlineData(3, IssueOutcome.QuestSuccess)]
    public void PeaceWithTheEnemyEndsTheQuestOnceAndPaysOnlyForThreeRaids(int raids, IssueOutcome outcome)
    {
        Accept();
        GiveRaidCredit(Server, raids);
        var before = ServerRewards();
        var completed = new List<QuestBase>();

        Server.Call(() =>
        {
            var quest = QuestOn(Server);
            Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
            CampaignEvents.OnQuestCompletedEvent.AddNonSerializedListener(this, (ended, _) => completed.Add(ended));

            CampaignEventDispatcher.Instance.OnMakePeace(Giver(Server).MapFaction,
                Server.GetRegisteredObject<Kingdom>(enemyId), MakePeaceAction.MakePeaceDetail.ByKingdomDecision);

            Assert.Same(quest, Assert.Single(completed));
            Assert.True(quest.IsFinalized);
        }, DialogAndTextHelpers);

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

    private void Accept()
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

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
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
        var paid = new Rewards(owner.Gold + RewardGold, owner.Renown + RenownBonus, owner.Influence + InfluenceBonus);
        Assert.Equal(pays ? paid : owner, after.Owner);
        Assert.Equal(before.Bystander, after.Bystander);
    }

    private Hero Giver(EnvironmentInstance instance) => instance.GetRegisteredObject<Hero>(scenario.NotableId);

    private Quest QuestOn(EnvironmentInstance instance) => Assert.IsType<Quest>(Giver(instance).Issue.IssueQuest);

    private readonly record struct Rewards(int Gold, float Renown, float Influence);
}
