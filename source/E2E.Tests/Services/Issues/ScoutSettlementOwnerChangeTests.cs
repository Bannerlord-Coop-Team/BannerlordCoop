using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Finalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;

namespace E2E.Tests.Services.Issues;

/// <summary>Runs Scout's real accepted quest listener on the unstarted server mirror.</summary>
public class ScoutSettlementOwnerChangeTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly string[] targetIds;
    private readonly string clanId;
    private readonly Quest serverQuest;
    private readonly Quest ownerQuest;
    private readonly ITestOutputHelper output;

    public ScoutSettlementOwnerChangeTests(ITestOutputHelper output)
    {
        this.output = output;
        environment = new E2ETestEnvironment(output);
        targetIds = Enumerable.Range(0, 3).Select(_ => environment.CreateRegisteredObject<Settlement>()).ToArray();
        var townIds = Enumerable.Range(0, 3).Select(_ => environment.CreateRegisteredObject<Town>()).ToArray();
        clanId = environment.CreateRegisteredObject<Clan>();
        var giverClanId = environment.CreateRegisteredObject<Clan>();
        var enemyClanId = environment.CreateRegisteredObject<Clan>();
        var kingdomId = environment.CreateRegisteredObject<Kingdom>();
        var enemyKingdomId = environment.CreateRegisteredObject<Kingdom>();
        scenario = new QuestScenario(environment, (instance, giver) => new Issue(giver,
            targetIds.Select(id => Get<Settlement>(instance, id)).ToList()));

        foreach (var instance in new[] { scenario.Server, scenario.ClientA, scenario.ClientB })
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    var clan = Get<Clan>(instance, clanId);
                    var giverClan = Get<Clan>(instance, giverClanId);
                    var enemyClan = Get<Clan>(instance, enemyClanId);
                    var kingdom = Get<Kingdom>(instance, kingdomId);
                    var enemyKingdom = Get<Kingdom>(instance, enemyKingdomId);
                    kingdom.Name = new TextObject("Allied kingdom");
                    enemyKingdom.Name = new TextObject("Enemy kingdom");
                    clan.Name = new TextObject("Allied clan");
                    clan._kingdom = kingdom;
                    clan.Tier = 2;
                    giverClan.Name = new TextObject("Commander's clan");
                    giverClan._kingdom = kingdom;
                    enemyClan._kingdom = enemyKingdom;
                    var giver = Get<Hero>(instance, scenario.NotableId);
                    giver.Clan = giverClan;
                    giverClan._leader = giver;
                    kingdom._rulingClan = giverClan;
                    var owner = Get<Hero>(instance, scenario.OwnerHeroId);
                    owner.Clan = clan;
                    owner._partyBelongedTo = Get<MobileParty>(instance, scenario.OwnerPartyId);
                    owner.SetSkillValue(DefaultSkills.Scouting, 60);

                    for (var i = 0; i < targetIds.Length; i++)
                    {
                        var settlement = Get<Settlement>(instance, targetIds[i]);
                        settlement._name = new TextObject("Scout target " + i);
                        var town = Get<Town>(instance, townIds[i]);
                        settlement.SetSettlementComponent(town);
                        town._ownerClan = enemyClan;
                    }

                    FactionManager.DeclareWar(kingdom, enemyKingdom);
                    FactionManager.DeclareWar(enemyKingdom, kingdom);
                    if (instance == scenario.ClientA)
                    {
                        Game.Current.PlayerTroop = owner.CharacterObject;
                        Campaign.Current.MainParty = owner.PartyBelongedTo;
                        Campaign.Current.PlayerDefaultFaction = clan;
                    }
                }
            });
        }

        scenario.Accept(scenario.ClientA);
        serverQuest = (Quest)scenario.QuestOf(scenario.Server);
        ownerQuest = (Quest)scenario.QuestOf(scenario.ClientA);
        scenario.ClientA.Call(() =>
        {
            using (new AllowedThread())
                ownerQuest.QuestAcceptedConsequences();
        });
        scenario.Server.Call(() => Assert.DoesNotContain(serverQuest, Campaign.Current.QuestManager.Quests));
    }

    public void Dispose() => environment.Dispose();

    [Fact]
    public void NeutralizingEveryTargetRunsTheNativeListenerAndCancelsTheOwnersQuest()
    {
        for (var i = 0; i < targetIds.Length; i++)
        {
            var index = i;
            scenario.Server.Call(() =>
            {
                var settlement = Get<Settlement>(scenario.Server, targetIds[index]);
                settlement.Town._ownerClan = Get<Clan>(scenario.Server, clanId);
                var owner = Get<Hero>(scenario.Server, scenario.OwnerHeroId);
                var giver = Get<Hero>(scenario.Server, scenario.NotableId);
                CampaignEventDispatcher.Instance.OnSettlementOwnerChanged(settlement, false, owner, giver, null,
                    ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege);
                Assert.NotNull(serverQuest._startQuestLog);
                Assert.Equal(index + 1, serverQuest._startQuestLog.CurrentProgress);
                Assert.Equal(index + 1, serverQuest._scoutedSettlementCount);
            });
        }

        environment.FlushCoalescer();
        var ending = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestCancel, ending.Outcome);
        Assert.Empty(scenario.ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        scenario.Server.Call(() =>
        {
            Assert.True(serverQuest.IsFinalized);
            Assert.All(new[] { serverQuest._questSettlement1, serverQuest._questSettlement2, serverQuest._questSettlement3 },
                target => Assert.True(target.IsCompletedThroughBeingNeutral));
            Assert.False(scenario.Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(
                scenario.NotableId, QuestScenario.IssueId, out _));
        });
        scenario.ClientA.Call(() =>
        {
            Assert.True(ownerQuest.IsFinalized);
            Assert.DoesNotContain(ownerQuest, Campaign.Current.QuestManager.Quests);
        });
        scenario.ClientB.Call(() => Assert.Empty(Campaign.Current.QuestManager.Quests));
    }

    [Fact]
    public void ReapplyingTheAcceptCapturePreservesTheServersExistingProgressLog()
    {
        scenario.ClientA.Call(() =>
        {
            foreach (var entry in ownerQuest.JournalEntries)
                Describe("owner", entry);
        });
        scenario.Server.Call(() =>
        {
            foreach (var entry in serverQuest.JournalEntries)
                Describe("server", entry);
            foreach (var request in scenario.Server.InternalMessages.GetMessages<RequestQuestJournal>())
                foreach (var entry in request.Entries)
                    Describe("wire", entry.ToJournalLog());
            var log = serverQuest._startQuestLog;
            Assert.NotNull(log);
            var issue = Get<Hero>(scenario.Server, scenario.NotableId).Issue;
            Assert.True(scenario.Server.Resolve<IQuestTypeRegistry>().TryGet(issue.GetType(), out var descriptor));
            descriptor.QuestSolutionAcceptStrategy.Apply(issue, descriptor.QuestSolutionAcceptStrategy.Capture(issue));
            Assert.Same(log, serverQuest._startQuestLog);
            Assert.Same(log, Assert.Single(serverQuest.JournalEntries));
        });
    }

    private void Describe(string side, JournalLog entry)
    {
        output.WriteLine("{0}: time={1} progress={2} range={3} type={4}", side,
            entry.LogTime.NumTicks, entry.CurrentProgress, entry.Range, entry.Type);
        output.WriteLine("{0} text: {1}", side, DescribeText(entry.LogText));
        output.WriteLine("{0} task: {1}", side, DescribeText(entry.TaskName));
    }

    private static string DescribeText(TextObject text)
    {
        if (text == null) return "<null>";
        var attributes = text.Attributes?.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key + "=" + (pair.Value is TextObject nested ? DescribeText(nested) :
                pair.Value?.GetType().FullName + ":" + pair.Value));
        return text.Value + " {" + string.Join(", ", attributes ?? Enumerable.Empty<string>()) + "}";
    }

    private static T Get<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }
}
