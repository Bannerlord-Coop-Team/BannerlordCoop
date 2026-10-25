using Common.Util;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>Separates a paused server-only daily callback from a native periodic day on both owners.</summary>
public class HeadmanHerdAlternativeProgressTests
{
    private const long InitialTicks = 79213041185336;
    private readonly ITestOutputHelper output;

    public HeadmanHerdAlternativeProgressTests(ITestOutputHelper output) => this.output = output;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConcurrentNativeAlternativesRetainTheSameReturnDayProgress(bool periodicDay)
    {
        lock (GameInstance.@lock)
        {
            using var herd = new HeadmanNeedsToDeliverAHerdTests(output);
            var now = AccessTools.PropertyGetter(typeof(CampaignTime), nameof(CampaignTime.Now));
            var patches = Harmony.GetPatchInfo(now).Prefixes.ToArray();
            var order = PatchProcessor.GetSortedPatchMethods(now, patches);
            var bootstrap = Assert.Single(patches, patch => patch.owner == "Coop.Testing");
            var harmony = new Harmony(bootstrap.owner);
            var variables = new Dictionary<string, TextObject>(MBTextManager.TextContext._variables);
            try
            {
                harmony.Unpatch(now, bootstrap.PatchMethod);
                AssertProgress(herd, periodicDay);
            }
            finally
            {
                MBTextManager.TextContext._variables.Clear();
                foreach (var variable in variables) MBTextManager.TextContext._variables.Add(variable.Key, variable.Value);
                harmony.Patch(now, prefix: new HarmonyMethod(bootstrap.PatchMethod)
                {
                    priority = bootstrap.priority,
                    before = bootstrap.before,
                    after = bootstrap.after,
                    debug = bootstrap.debug,
                });
                Assert.Equal(order, PatchProcessor.GetSortedPatchMethods(now, Harmony.GetPatchInfo(now).Prefixes.ToArray()));
            }
        }
    }

    private void AssertProgress(HeadmanNeedsToDeliverAHerdTests herd, bool periodicDay)
    {
        var scenario = herd.scenario;
        var instances = new[] { scenario.Server, scenario.ClientA, scenario.ClientB };
        var otherGiver = scenario.AddIssue();
        var companionA = herd.environment.CreateRegisteredObject<Hero>();
        var companionB = herd.environment.CreateRegisteredObject<Hero>();
        var troop = herd.environment.CreateRegisteredObject<CharacterObject>();
        var upgrade = herd.environment.CreateRegisteredObject<CharacterObject>();
        foreach (var instance in instances)
            instance.Call(() =>
            {
                new JournalLogsCampaignBehavior().RegisterEvents();
                Campaign.Current.MapTimeTracker._numTicks = InitialTicks;
                Assert.Equal(InitialTicks, CampaignTime.Now.NumTicks);
                using var setup = new AllowedThread();
                Get<CharacterObject>(instance, troop).Level = 16;
                Get<CharacterObject>(instance, troop).UpgradeTargets = new[] { Get<CharacterObject>(instance, upgrade) };
                Get<CharacterObject>(instance, upgrade).Level = 21;
                Get<CharacterObject>(instance, upgrade).UpgradeTargets = Array.Empty<CharacterObject>();
                foreach (var pair in new[] { (scenario.PlayerA, companionA, "Shulun"), (scenario.PlayerB, companionB, "Phestos") })
                {
                    var hero = Get<Hero>(instance, pair.Item2);
                    hero.SetName(new TextObject(pair.Item3), new TextObject(pair.Item3));
                    hero.Clan = Get<Hero>(instance, pair.Item1.HeroId).Clan;
                    hero._partyBelongedTo = Get<MobileParty>(instance, pair.Item1.MobilePartyId);
                    hero._heroState = Hero.CharacterStates.Active;
                }
            });
        scenario.Server.Call(() =>
        {
            foreach (var pair in new[] { (scenario.PlayerA, companionA), (scenario.PlayerB, companionB) })
            {
                var roster = Get<MobileParty>(scenario.Server, pair.Item1.MobilePartyId).MemberRoster;
                roster.AddToCounts(Get<Hero>(scenario.Server, pair.Item2).CharacterObject, 1);
                roster.AddToCounts(Get<CharacterObject>(scenario.Server, troop), 8);
                roster.AddXpToTroop(Get<CharacterObject>(scenario.Server, troop), 1250);
            }
        });
        herd.environment.FlushCoalescer();

        // R7 accepted B first, then A, through native picker commitments rather than assigning taken state.
        Accept(scenario.ClientB, otherGiver, scenario.OtherHeroId, scenario.OtherPartyId, companionB);
        Accept(scenario.ClientA, scenario.NotableId, scenario.OwnerHeroId, scenario.OwnerPartyId, companionA);
        herd.environment.FlushCoalescer();
        var accepts = scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>().ToArray();
        Assert.Equal(2, accepts.Length);
        Assert.Equal(QuestScenario.ControllerB, Assert.Single(accepts, message => message.IssueOwnerId == otherGiver).ControllerId);
        Assert.Equal(QuestScenario.ControllerA, Assert.Single(accepts, message => message.IssueOwnerId == scenario.NotableId).ControllerId);

        JournalLog[] baselineA = null!;
        var controls = new Dictionary<EnvironmentInstance, (string Roster, string Items, int Gold, string OtherItems, int OtherGold, long Clock)>();
        var sentXp = 0;
        scenario.Server.Call(() =>
        {
            baselineA = Get<Hero>(scenario.Server, scenario.NotableId).Issue.JournalEntries
                .Select(log => new AlternativeSolutionJournalEntry(log).ToJournalLog()).ToArray();
            var issueB = Get<Hero>(scenario.Server, otherGiver).Issue;
            sentXp = StackXp(issueB.AlternativeSolutionSentTroops, Get<CharacterObject>(scenario.Server, troop));
            Assert.True(sentXp > 0);
            Assert.Equal(9, issueB.AlternativeSolutionSentTroops.TotalManCount);
        });
        foreach (var instance in instances)
            instance.Call(() =>
            {
                var issueA = Get<Hero>(instance, scenario.NotableId).Issue;
                Assert.True(issueA.IsSolvingWithAlternative);
                Assert.Null(issueA.IssueQuest);
                var log = Assert.Single(issueA.JournalEntries);
                Assert.Equal(0, log.CurrentProgress);
                if (instance != scenario.ClientB)
                {
                    Assert.Equal("VFO7rMzK", log.TaskName.GetID());
                    HeadmanHerdCancellationCompositionTests.AssertHistory(baselineA, issueA.JournalEntries);
                }
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, issueA.StringId, out var owner));
                Assert.Equal(QuestScenario.ControllerA, owner);
                var party = Get<MobileParty>(instance, scenario.OwnerPartyId);
                controls[instance] = (Roster(party), Items(party), Get<Hero>(instance, scenario.OwnerHeroId).Gold,
                    Items(Get<MobileParty>(instance, scenario.OtherPartyId)), Get<Hero>(instance, scenario.OtherHeroId).Gold, CampaignTime.Now.NumTicks);
                if (periodicDay)
                {
                    // Use the native timer and daily callback; never invoke a client callback to repair the paused row.
                    Campaign.Current._dailyTickEvent = CampaignPeriodicEventManager.CreatePeriodicEvent(CampaignTime.Days(1), CampaignTime.Days(1));
                    Campaign.Current._dailyTickEvent.AddHandler(Campaign.Current.DailyTick);
                }
            });
        scenario.Server.Call(() =>
        {
            var issueB = Get<Hero>(scenario.Server, otherGiver).Issue;
            issueB._failureChance = 1f;
            issueB.AlternativeSolutionReturnTimeForTroops = CampaignTime.DaysFromNow(-1);
            issueB.IssueDueTime = issueB.AlternativeSolutionReturnTimeForTroops;
            if (!periodicDay) Campaign.Current.IssueManager.DailyTick();
        });
        if (periodicDay)
            foreach (var instance in instances)
                instance.Call(() =>
                {
                    Campaign.Current.MapTimeTracker.Tick((float)CampaignTime.Days(1).ToSeconds + 1f);
                    Campaign.Current._campaignPeriodicEventManager.OnTick(1f);
                });
        herd.environment.FlushCoalescer();

        var ending = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.AlternativeSolution, ending.Outcome);
        var report = Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionJournal>(),
            message => message.Ending == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest);
        Assert.Equal(otherGiver, report.IssueOwnerId);
        Assert.Equal(2, report.Entries.Length);
        Assert.Contains("Phestos", report.Entries[1].Text);
        Assert.DoesNotContain("Shulun", report.Entries[1].Text);
        JournalLog[] afterA = null!;
        scenario.Server.Call(() =>
        {
            var issueA = Get<Hero>(scenario.Server, scenario.NotableId).Issue;
            var log = Assert.Single(issueA.JournalEntries);
            Assert.Equal(1, log.CurrentProgress);
            afterA = issueA.JournalEntries.ToArray();
            var before = baselineA[0];
            HeadmanHerdCancellationCompositionTests.AssertHistory(new[]
            {
                new JournalLog(before.LogTime, before.LogText, before.TaskName, 1, before.Range, before.Type),
            }, afterA);
            Assert.Equal(sentXp, StackXp(Get<MobileParty>(scenario.Server, scenario.OtherPartyId).MemberRoster,
                Get<CharacterObject>(scenario.Server, troop)));
        });
        foreach (var instance in instances)
            instance.Call(() =>
            {
                var party = Get<MobileParty>(instance, scenario.OwnerPartyId);
                Assert.Equal(controls[instance].Roster, Roster(party));
                Assert.Equal(controls[instance].Items, Items(party));
                Assert.Equal(controls[instance].Gold, Get<Hero>(instance, scenario.OwnerHeroId).Gold);
                Assert.Equal(controls[instance].OtherItems, Items(Get<MobileParty>(instance, scenario.OtherPartyId)));
                Assert.Equal(controls[instance].OtherGold, Get<Hero>(instance, scenario.OtherHeroId).Gold);
                if (!periodicDay) Assert.Equal(controls[instance].Clock, CampaignTime.Now.NumTicks);
                Assert.Null(Get<Hero>(instance, otherGiver).Issue);
            });
        scenario.ClientB.Call(() =>
        {
            var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(log => log._relatedObjectIds.Contains(ending.IssueId)));
            Assert.Equal(IssueBase.IssueUpdateDetails.SentTroopsFailedQuest, archive._lastIssueStatus);
            Assert.True(archive.IsEnded());
            Assert.Equal(2, archive.GetEntries().Count());
            var party = Get<MobileParty>(scenario.ClientB, scenario.OtherPartyId);
            Assert.Equal(8, party.MemberRoster.GetTroopCount(Get<CharacterObject>(scenario.ClientB, troop)));
            Assert.Equal(1, party.MemberRoster.GetTroopCount(Get<Hero>(scenario.ClientB, companionB).CharacterObject));
            Assert.Equal(sentXp, StackXp(party.MemberRoster, Get<CharacterObject>(scenario.ClientB, troop)));
        });
        scenario.ClientA.Call(() => Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
            log => log._relatedObjectIds.Contains(ending.IssueId))));
        output.WriteLine($"Native Return Days oracle: periodicDay={periodicDay}, server=1; paused R7 runs only one global server callback. No animal count or foreign owner scope writes this field.");
        scenario.ClientA.Call(() => HeadmanHerdCancellationCompositionTests.AssertHistory(afterA,
            Get<Hero>(scenario.ClientA, scenario.NotableId).Issue.JournalEntries));

        void Accept(EnvironmentInstance client, string giverId, string heroId, string partyId, string companionId)
        {
            client.Call(() =>
            {
                var issue = Get<Hero>(client, giverId).Issue;
                var owner = Get<Hero>(client, heroId);
                var party = Get<MobileParty>(client, partyId);
                using var scope = new MainHeroSubstitutionScope(owner, party);
                using var difficulty = new IssueDifficultyOverride(0.1f);
                Assert.Equal(8, issue.GetTotalAlternativeSolutionNeededMenCount());
                Assert.True(issue.AlternativeSolutionCondition(out _));
                using (new AllowedThread()) issue.AlternativeSolutionSentTroops.AddToCounts(Get<Hero>(client, companionId).CharacterObject, 1);
                AlternativeSolutionPickerDriver.Commit(client, issue, owner, party, Get<CharacterObject>(client, troop), 8);
                issue.StartIssueWithAlternativeSolution();
            });
        }
    }

    private static int StackXp(TaleWorlds.CampaignSystem.Roster.TroopRoster roster, CharacterObject troop)
        => roster.GetElementCopyAtIndex(roster.FindIndexOfTroop(troop)).Xp;

    private static string Roster(MobileParty party) => string.Join(";", party.MemberRoster.GetTroopRoster()
        .Select(element => $"{element.Character.StringId}:{element.Number}:{element.WoundedNumber}:{element.Xp}"));

    private static string Items(MobileParty party) => string.Join(";", party.ItemRoster
        .Select(element => $"{element.EquipmentElement.Item.StringId}:{element.EquipmentElement.ItemModifier?.StringId}:{element.Amount}"));

    private static T Get<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }
}
