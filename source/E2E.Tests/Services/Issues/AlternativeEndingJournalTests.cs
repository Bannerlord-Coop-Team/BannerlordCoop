using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Definition;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.SaveSystem.Save;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>Checks native alternative failure text through immediate replay and saved history.</summary>
public class AlternativeEndingJournalTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly Dictionary<string, TextObject> textVariables;

    public AlternativeEndingJournalTests(ITestOutputHelper output)
    {
        lock (GameInstance.@lock)
            textVariables = new Dictionary<string, TextObject>(MBTextManager.TextContext._variables);
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
    }

    public void Dispose()
    {
        try
        {
            environment.Dispose();
        }
        finally
        {
            lock (GameInstance.@lock)
            {
                MBTextManager.TextContext._variables.Clear();
                foreach (var variable in textVariables)
                    MBTextManager.TextContext._variables.Add(variable.Key, variable.Value);
            }
        }
    }

    [Fact]
    public void NamedCompanionFailureSurvivesGlobalChangesAndNativeSaveLoadOnlyForItsOwner()
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
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    companion.SetName(new TextObject("Mission Companion"), new TextObject("Mission"));
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
            });
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
            Assert.Contains("Mission Companion", expected[1]);
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var party));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(scenario.Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
            Assert.Equal(8, party.MemberRoster.GetTroopCount(troop));
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
        });
        environment.FlushCoalescer();

        var journal = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionJournal>());
        Assert.Equal(IssueBase.IssueUpdateDetails.SentTroopsFailedQuest, journal.Ending);
        Assert.Equal(QuestScenario.IssueId, journal.IssueId);
        Assert.Equal(scenario.NotableId, journal.IssueOwnerId);
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

        LogEntryHistory loaded = null!;
        scenario.Server.Call(() =>
        {
            var replacement = new TextObject("");
            replacement.SetTextVariable("LINK", "A Different Companion");
            MBTextManager.SetTextVariable("COMPANION", replacement);
            Assert.Contains("A Different Companion", new TextObject("{COMPANION.LINK}").ToString());
            var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId)));
            var definitions = new DefinitionContext();
            definitions.FillWithCurrentTypes();
            Assert.False(definitions.GotError, string.Join("\n", definitions.Errors));
            var save = new SaveContext(definitions);
            var metadata = new MetaData();
            Assert.True(save.Save(Campaign.Current.LogEntryHistory, metadata, out var error), error);
            var driver = new InMemDriver();
            Assert.Equal(SaveResult.Success, driver.Save("alternative_companion", 1, metadata, save.SaveData)
                .GetAwaiter().GetResult().SaveResult);
            var load = new LoadContext(definitions, driver);
            Assert.True(load.Load(driver.Load("alternative_companion"), loadAsLateInitialize: true));
            loaded = Assert.IsType<LogEntryHistory>(load.RootObject);
            Assert.Equal(expected, archive.GetEntries().Select(entry => entry.LogText.ToString()).ToArray());
        });
        scenario.ClientA.Call(() =>
        {
            var archive = Assert.Single(loaded.GetGameActionLogs<JournalLogEntry>(
                entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId) && entry.IsEnded()));
            Assert.Equal(IssueBase.IssueUpdateDetails.SentTroopsFailedQuest, archive._lastIssueStatus);
            Assert.Equal(expected, archive.GetEntries().Select(entry => entry.LogText.ToString()).ToArray());
        });
        scenario.ClientB.Call(() => Assert.Empty(loaded.GetGameActionLogs<JournalLogEntry>(
            entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId) && entry.IsEnded())));
    }
}
