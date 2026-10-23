using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>Checks the owner's archive immediately after native alternative failure.</summary>
public class AlternativeEndingJournalTests : IDisposable
{
    // Synthetic heroes have no encyclopedia entries; the failure and journal callbacks still run.
    private static readonly MethodBase[] JournalTextLinks =
    {
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.GetCharacterProperties)),
    };

    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;

    public AlternativeEndingJournalTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
    }

    public void Dispose()
    {
        environment.Dispose();
    }

    [Fact]
    public void FailedAlternativeImmediatelyArchivesTheFullJournalOnlyForItsOwner()
    {
        var companionId = environment.CreateRegisteredObject<Hero>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        var rosterId = environment.CreateRegisteredObject<TroopRoster>();
        foreach (var instance in environment.Clients.Prepend(environment.Server))
        {
            instance.Call(() =>
            {
                new JournalLogsCampaignBehavior().RegisterEvents();
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(scenario.OwnerHeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.True(instance.ObjectManager.TryGetObject<TroopRoster>(rosterId, out var roster));
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TrySetOwner(
                    scenario.NotableId, QuestScenario.IssueId, QuestScenario.ControllerA));
                using (new AllowedThread())
                {
                    party.Party.MemberRoster = roster;
                    roster.OwnerParty = party.Party;
                    owner.PartyBelongedTo = party;
                    giver.Issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                    giver.Issue._failureChance = 1f;
                    giver.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                    giver.Issue.AlternativeSolutionSentTroops.AddToCounts(troop, 8);
                    companion._heroState = Hero.CharacterStates.Disabled;
                }
                if (instance != scenario.ClientB)
                {
                    using (new MainHeroSubstitutionScope(owner, party))
                        giver.Issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject("Troops sent to resolve the issue.")));
                }
            }, JournalTextLinks);
        }

        string[] expected = null!;
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(scenario.NotableId, out var giver));
            giver.Issue.CompleteIssueWithAlternativeSolution();
            var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId)));
            Assert.Equal(IssueBase.IssueUpdateDetails.SentTroopsFailedQuest, archive._lastIssueStatus);
            Assert.True(archive.IsEnded());
            expected = archive.GetEntries().Select(entry => entry.LogText.ToString()).ToArray();
            Assert.Equal(2, expected.Length);
            Assert.Contains("has failed to resolve the matter. 9 troops came back safe and sound.", expected[1]);
        }, JournalTextLinks);
        environment.FlushCoalescer();

        var finalized = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.AlternativeSolution, finalized.Outcome);
        Assert.Equal(QuestScenario.IssueId, finalized.IssueId);
        scenario.ClientB.Call(() => Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
            entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId))));
        scenario.ClientA.Call(() =>
        {
            var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId) && entry.IsEnded()));
            Assert.Equal(IssueBase.IssueUpdateDetails.SentTroopsFailedQuest, archive._lastIssueStatus);
            Assert.Equal(expected, archive.GetEntries().Select(entry => entry.LogText.ToString()).ToArray());
        });
    }
}
