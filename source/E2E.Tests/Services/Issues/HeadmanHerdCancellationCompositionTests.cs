using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using TaleWorlds.SaveSystem.Definition;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.SaveSystem.Save;
using Xunit.Abstractions;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

namespace E2E.Tests.Services.Issues;

public class HeadmanHerdCancellationCompositionTests : IDisposable
{
    private readonly HeadmanNeedsToDeliverAHerdTests herd;
    private readonly ITestOutputHelper output;
    private readonly Dictionary<string, TextObject> textVariables;
    private QuestScenario Scenario => herd.scenario;
    private EnvironmentInstance[] Instances => new[] { Scenario.Server, Scenario.ClientA, Scenario.ClientB };

    public HeadmanHerdCancellationCompositionTests(ITestOutputHelper output)
    {
        this.output = output;
        lock (GameInstance.@lock)
            textVariables = new Dictionary<string, TextObject>(MBTextManager.TextContext._variables);
        herd = new HeadmanNeedsToDeliverAHerdTests(output);
        foreach (var instance in Instances)
            instance.Call(() => new JournalLogsCampaignBehavior().RegisterEvents());
    }

    public void Dispose()
    {
        try { herd.Dispose(); }
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealLowPowerGiverDeathPreservesTheOwnersFullNativeCancellationHistory(bool savedHistory)
    {
        var (companionId, troopId) = herd.StartCompanions();
        var otherGiverId = Scenario.AddIssue();
        Scenario.ClientB.Call(() =>
        {
            using (new MainHeroSubstitutionScope(Object<Hero>(Scenario.ClientB, Scenario.OtherHeroId),
                Object<MobileParty>(Scenario.ClientB, Scenario.OtherPartyId)))
            {
                var other = Object<Hero>(Scenario.ClientB, otherGiverId).Issue;
                other.StartIssueWithQuest();
                Assert.IsType<Quest>(other.IssueQuest).QuestAcceptedConsequences();
            }
        });
        Scenario.Server.Call(() =>
        {
            var troop = Object<CharacterObject>(Scenario.Server, troopId);
            var sent = Object<Hero>(Scenario.Server, Scenario.NotableId).Issue.AlternativeSolutionSentTroops;
            sent.AddToCounts(troop, 0, false, 1, 828);
            Object<MobileParty>(Scenario.Server, Scenario.OtherPartyId).MemberRoster.AddToCounts(troop, 7, false, 1, 313);
        });
        herd.environment.FlushCoalescer();

        var otherEntries = new Dictionary<EnvironmentInstance, JournalLog[]>();
        var otherGold = new Dictionary<EnvironmentInstance, int>();
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                var other = Object<Hero>(instance, otherGiverId).Issue;
                otherEntries[instance] = other.IssueQuest?.JournalEntries.ToArray() ?? Array.Empty<JournalLog>();
                otherGold[instance] = Object<Hero>(instance, Scenario.OtherHeroId).Gold;
            });

        JournalLogEntry authoritative = null!;
        Scenario.Server.Call(() =>
        {
            var giver = Object<Hero>(Scenario.Server, Scenario.NotableId);
            Assert.True(giver.IsAlive);
            Assert.True(giver.Power < Campaign.Current.Models.NotablePowerModel.NotableDisappearPowerLimit);
            Assert.Contains(Campaign.Current.IssueManager, CampaignEventDispatcher.Instance._eventReceivers);
            Assert.True(giver.Issue.IsSolvingWithAlternative);
            Assert.Equal(41, giver.Issue.AlternativeSolutionSentTroops.TotalManCount);
            Assert.Equal(IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest, Archive(Campaign.Current.LogEntryHistory)._lastIssueStatus);

            // The bootstrap suppresses ChangeState, including the native low-power replacement listeners.
            var changeState = AccessTools.Method(typeof(Hero), nameof(Hero.ChangeState));
            var bootstrap = Assert.Single(Harmony.GetPatchInfo(changeState).Prefixes, patch => patch.owner == "Coop.Testing");
            var harmony = new Harmony(bootstrap.owner);
            harmony.Unpatch(changeState, bootstrap.PatchMethod);
            try
            {
                giver.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.DiedOfOldAge);
                KillCharacterAction.ApplyByDeathMarkForced(giver, false);
            }
            finally
            {
                harmony.Patch(changeState, prefix: new HarmonyMethod(bootstrap.PatchMethod)
                {
                    priority = bootstrap.priority,
                    before = bootstrap.before,
                    after = bootstrap.after,
                });
            }
            Assert.False(giver.IsAlive);
            Assert.Null(giver.Issue);
            authoritative = Archive(Campaign.Current.LogEntryHistory);
            Assert.True(authoritative.IsEnded());
            Assert.Equal(IssueBase.IssueUpdateDetails.IssueCancel, authoritative._lastIssueStatus);
            Assert.Single(authoritative.GetEntries(), log => log.LogText.GetID() == "V5Za6d4h");
            output.WriteLine("Native server cancellation: status={0}, stages={1}", authoritative._lastIssueStatus, authoritative.GetEntries().Count());
        }, new[] { AccessTools.Method(typeof(KillCharacterAction), nameof(KillCharacterAction.CreateObituary)) });
        herd.environment.FlushCoalescer();

        LogEntryHistory loaded = null!;
        if (savedHistory)
        {
            Scenario.Server.Call(() =>
            {
                MBTextManager.SetTextVariable("COMPANION", new TextObject("Changed global companion"));
                MBTextManager.SetTextVariable("ISSUE_GIVER", new TextObject("Changed global giver"));
                var definitions = new DefinitionContext();
                definitions.FillWithCurrentTypes();
                Assert.False(definitions.GotError, string.Join("\n", definitions.Errors));
                var save = new SaveContext(definitions);
                var metadata = new MetaData();
                Assert.True(save.Save(Campaign.Current.LogEntryHistory, metadata, out var error), error);
                var driver = new InMemDriver();
                Assert.Equal(SaveResult.Success, driver.Save("herd_alternative_cancel", 1, metadata, save.SaveData).GetAwaiter().GetResult().SaveResult);
                var load = new LoadContext(definitions, driver);
                Assert.True(load.Load(driver.Load("herd_alternative_cancel"), loadAsLateInitialize: true));
                loaded = Assert.IsType<LogEntryHistory>(load.RootObject);
                Assert.NotSame(Campaign.Current.LogEntryHistory, loaded);
            });
        }

        Scenario.ClientA.Call(() =>
        {
            var owner = Archive(savedHistory ? loaded : Campaign.Current.LogEntryHistory);
            output.WriteLine("Owner raw history before UI: saved={0}, ended={1}, status={2}, stages={3}",
                savedHistory, owner.IsEnded(), owner._lastIssueStatus, owner.GetEntries().Count());
            Assert.True(owner.IsEnded(), "The owner's raw cancellation archive must be ended immediately, before opening the journal or rejoining.");
            Assert.Equal(authoritative._lastIssueStatus, owner._lastIssueStatus);
            Assert.Equal(authoritative._relatedObjectIds, owner._relatedObjectIds);
            AssertHistory(authoritative.GetEntries(), owner.GetEntries());
        });
        output.WriteLine("PASS full native owner history ({0})", savedHistory ? "SaveContext/InMemDriver/LoadContext" : "immediate");

        // Keep mechanics after the history assertion so a red archive cannot imply these checks ran.
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Scenario.Server.Call(() => CampaignEventDispatcher.Instance.HourlyTick());
        herd.environment.FlushCoalescer();
        Assert.Single(Scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        foreach (var instance in Instances)
            instance.Call(() =>
            {
                Assert.Null(Object<Hero>(instance, Scenario.NotableId).Issue);
                Assert.False(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwner(Scenario.NotableId, QuestScenario.IssueId, out _));
                var returned = Object<MobileParty>(instance, Scenario.OwnerPartyId).MemberRoster;
                var troop = Object<CharacterObject>(instance, troopId);
                var stack = returned.GetElementCopyAtIndex(returned.FindIndexOfTroop(troop));
                Assert.Equal(40, stack.Number);
                Assert.Equal(1, stack.WoundedNumber);
                Assert.Equal(instance == Scenario.ClientB ? 0 : 828, stack.Xp);
                Assert.Equal(1, returned.GetTroopCount(Object<Hero>(instance, companionId).CharacterObject));
                Assert.Equal(Hero.CharacterStates.Active, Object<Hero>(instance, companionId).HeroState);
                var other = Object<Hero>(instance, otherGiverId).Issue;
                Assert.True(other.IsSolvingWithQuest);
                AssertHistory(otherEntries[instance], other.IssueQuest?.JournalEntries ?? Enumerable.Empty<JournalLog>());
                Assert.Equal(otherGold[instance], Object<Hero>(instance, Scenario.OtherHeroId).Gold);
                var otherRoster = Object<MobileParty>(instance, Scenario.OtherPartyId).MemberRoster;
                Assert.Equal(7, otherRoster.GetTroopCount(troop));
                var otherStack = otherRoster.GetElementCopyAtIndex(otherRoster.FindIndexOfTroop(troop));
                Assert.Equal(1, otherStack.WoundedNumber);
                Assert.Equal(instance == Scenario.ClientA ? 0 : 313, otherStack.Xp);
                if (instance == Scenario.Server)
                    Assert.Empty(Campaign.Current.IssueManager._awaitingAlternativeSolutionTroops.GetTroopRoster());
            });
        Scenario.ClientB.Call(() =>
        {
            Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId)));
            if (savedHistory)
                Assert.Empty(loaded.GetGameActionLogs<JournalLogEntry>(entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId)));
        });
        output.WriteLine("PASS one finalization, one companion/troop return after hourly retry, cleared ownership, controller XP, observer WithoutXP, and unchanged bystander quest/roster/gold");
    }

    private static JournalLogEntry Archive(LogEntryHistory history) => Assert.Single(history.GetGameActionLogs<JournalLogEntry>(
        entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId)));

    private static void AssertHistory(IEnumerable<JournalLog> expected, IEnumerable<JournalLog> actual)
    {
        var left = expected.ToArray();
        var right = actual.ToArray();
        Assert.Equal(left.Length, right.Length);
        for (var index = 0; index < left.Length; index++)
        {
            Assert.Equal(left[index].LogTime, right[index].LogTime);
            Assert.Equal(left[index].CurrentProgress, right[index].CurrentProgress);
            Assert.Equal(left[index].Range, right[index].Range);
            Assert.Equal(left[index].Type, right[index].Type);
            AssertText(left[index].LogText, right[index].LogText);
            AssertText(left[index].TaskName, right[index].TaskName);
        }
    }

    private static void AssertText(TextObject? expected, TextObject? actual)
    {
        if (expected == null) { Assert.Null(actual); return; }
        Assert.NotNull(actual);
        Assert.Equal(expected.Value, actual.Value);
        Assert.Equal(expected.ToString(), actual.ToString());
        Assert.Equal(expected.Attributes == null, actual.Attributes == null);
        if (expected.Attributes == null) return;
        Assert.Equal(expected.Attributes.Keys.OrderBy(key => key), actual.Attributes.Keys.OrderBy(key => key));
        foreach (var attribute in expected.Attributes)
        {
            if (attribute.Value is TextObject text)
                AssertText(text, Assert.IsType<TextObject>(actual.Attributes[attribute.Key]));
            else
                Assert.Equal(attribute.Value, actual.Attributes[attribute.Key]);
        }
    }

    private static T Object<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }
}
