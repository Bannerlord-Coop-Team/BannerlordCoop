using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Registries;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>An alternative solution whose completion throws applies its outcome once and ends, instead of again on every tick.</summary>
public class AlternativeSolutionCompletionGuardTests : IDisposable
{
    private static readonly MethodBase TroopReturnMethod =
        AccessTools.Method(typeof(IssueManager), nameof(IssueManager.MakeAlternativeTroopsReturn));

    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly Dictionary<string, TextObject> textVariables;
    private readonly string companionId;
    private readonly string troopId;
    private readonly List<IssueBase.IssueUpdateDetails> updates = new();

    private IEnumerable<EnvironmentInstance> Instances => environment.Clients.Prepend(environment.Server);

    public AlternativeSolutionCompletionGuardTests(ITestOutputHelper output)
    {
        // Vanilla's failure report binds COMPANION into the global text context
        lock (GameInstance.@lock)
            textVariables = new Dictionary<string, TextObject>(MBTextManager.TextContext._variables);
        environment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(environment);
        companionId = environment.CreateRegisteredObject<Hero>();
        troopId = environment.CreateRegisteredObject<CharacterObject>();
        scenario.SendTroops(companionId, troopId);
        scenario.Server.Call(() =>
        {
            // Set to fail and due back already, only the server's daily tick reads these
            var issue = scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
            issue._failureChance = 1f;
            issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.DaysFromNow(-1f);
        });
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                }
            });
        }
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
    public void CompanionWhoseDeathAlreadyRanStaysBehindAndTheTroopsReturnOnce()
    {
        using var nativeState = new NativeHeroStateScope();
        scenario.Server.Call(() =>
        {
            var companion = scenario.Server.GetRegisteredObject<Hero>(companionId);
            using (new AllowedThread())
            {
                companion.SetBirthDay(CampaignTime.YearsFromNow(-30f));
                companion._defaultAge = 30f;
                // What vanilla's death clears, while the hero stays Disabled on the mission like in #4887
                companion.OnDeath();
            }
            new CharacterDevelopmentCampaignBehavior().RegisterEvents();
            Assert.False(companion.IsChild);
            Assert.Null(companion.HeroDeveloper);
            Assert.Equal(Hero.CharacterStates.Disabled, companion.HeroState);
        });

        var errors = TickThreeDays();

        // Each tick on development throws the report's NRE from CharacterDevelopmentCampaignBehavior.OnHeroActivated
        Assert.Empty(errors);
        AssertEndedOnce();
        scenario.Server.Call(() =>
        {
            var companion = scenario.Server.GetRegisteredObject<Hero>(companionId);
            var roster = scenario.Server.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId).MemberRoster;
            Assert.Equal(Hero.CharacterStates.Disabled, companion.HeroState);
            Assert.Equal(0, roster.GetTroopCount(companion.CharacterObject));
            Assert.Equal(3, roster.GetTroopCount(scenario.Server.GetRegisteredObject<CharacterObject>(troopId)));
        });
    }

    [Fact]
    public void ThrowingTroopReturnEndsTheMissionOnceAndFreesTheCompanion()
    {
        var errors = TickThreeDays(TroopReturnMethod);

        Assert.Empty(errors);
        AssertEndedOnce();
        scenario.Server.Call(() =>
            Assert.Equal(Hero.CharacterStates.Active, scenario.Server.GetRegisteredObject<Hero>(companionId).HeroState));
    }

    [Fact]
    public void ThrowingReadinessCheckGivesTheServerItsOwnHeroBackAndKeepsTheTroopsOut()
    {
        MethodBase readiness = null;
        Hero mainHero = null;
        MobileParty mainParty = null;
        Clan playerClan = null;
        scenario.Server.Call(() =>
        {
            readiness = AccessTools.Method(Campaign.Current.Models.IssueModel.GetType(),
                nameof(IssueModel.CanTroopsReturnFromAlternativeSolution));
            (mainHero, mainParty, playerClan) = (Hero.MainHero, MobileParty.MainParty, Clan.PlayerClan);
            Assert.NotSame(mainParty, scenario.Server.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId));
        });

        var errors = TickThreeDays(readiness);

        // It throws after the owner was substituted in and before the patch holds the scope
        Assert.Equal(3, errors.Count);
        scenario.Server.Call(() =>
        {
            Assert.Same(mainHero, Hero.MainHero);
            Assert.Same(mainParty, MobileParty.MainParty);
            Assert.Same(playerClan, Clan.PlayerClan);
            Assert.True(scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId).Issue.IsSolvingWithAlternative);
        });
        Assert.Empty(updates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IssueThatCannotBeDetachedIsNeverRunAgainAndEndsOnceItCanBe(bool ownerEntryStays)
    {
        // Both IssueFinalized and the deactivation fallback go through DeactivateIssue, so the issue stays attached.
        // With its owner entry kept too, only the claim keeps vanilla's completion from running again.
        var throwing = new List<MethodBase> { TroopReturnMethod, AccessTools.Method(typeof(IssueManager), nameof(IssueManager.DeactivateIssue)) };
        if (ownerEntryStays) throwing.Add(AccessTools.Method(typeof(IssueOwnershipRegistry), nameof(IssueOwnershipRegistry.Remove)));
        var errors = TickThreeDays(throwing.ToArray());

        Assert.Empty(errors);
        Assert.Equal(IssueBase.IssueUpdateDetails.SentTroopsFailedQuest, Assert.Single(updates));
        scenario.Server.Call(() =>
            Assert.NotNull(scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId).Issue));
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());

        // Once it can be detached, the claim ends it on the next tick without running vanilla again
        Assert.Empty(TickThreeDays(TroopReturnMethod));
        AssertEndedOnce();
    }

    [Fact]
    public void IssueTheManagerStillHoldsIsNotAnnouncedUntilItIsRemoved()
    {
        // Vanilla's DeactivateIssue clears the hero's pointer before RemoveRelatedLines and the manager removal
        var errors = TickThreeDays(TroopReturnMethod,
            AccessTools.Method(typeof(ConversationManager), nameof(ConversationManager.RemoveRelatedLines)));

        Assert.Empty(errors);
        Assert.Equal(IssueBase.IssueUpdateDetails.SentTroopsFailedQuest, Assert.Single(updates));
        scenario.Server.Call(() =>
        {
            var notable = scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId);
            Assert.Null(notable.Issue);
            Assert.True(Campaign.Current.IssueManager.Issues.ContainsKey(notable));
        });
        Assert.Empty(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());

        Assert.Empty(TickThreeDays(TroopReturnMethod));
        AssertEndedOnce();
        scenario.Server.Call(() => Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(
            scenario.Server.GetRegisteredObject<Hero>(scenario.NotableId))));
    }

    public enum FailurePoint { Consequence, EndingListener, CancelListener, TroopReturn, ConsequenceThenTroopReturn }

    [Theory]
    [InlineData(FailurePoint.Consequence)]
    [InlineData(FailurePoint.EndingListener)]
    [InlineData(FailurePoint.CancelListener)]
    [InlineData(FailurePoint.TroopReturn)]
    [InlineData(FailurePoint.ConsequenceThenTroopReturn)]
    public void ThrowAfterAcceptanceLeavesTheOwnersJournalEndedOnce(FailurePoint failure)
    {
        var instances = new[] { scenario.Server, scenario.ClientA };
        var accepted = new Dictionary<EnvironmentInstance, string[]>();
        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                var issue = instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue;
                var owner = instance.GetRegisteredObject<Hero>(scenario.OwnerHeroId);
                new JournalLogsCampaignBehavior().RegisterEvents();
                using (new AllowedThread()) issue.IsTriedToSolveBefore = true;
                using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
                {
                    issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject("Troops sent to resolve the issue.")));
                    CampaignEventDispatcher.Instance.OnIssueUpdated(issue, IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest, owner);
                }
                var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(entry => entry.IsRelatedTo(issue)));
                Assert.False(archive.IsEnded());
                accepted[instance] = archive.GetEntries().Select(entry => entry.LogText.ToString()).ToArray();
                // Added last so it runs before the journal listener, after the owner was already sent the ending
                if (instance == scenario.Server && failure == FailurePoint.EndingListener)
                    CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(this, (_, detail, _) =>
                    {
                        if (detail == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest) Throw();
                    });
                // The same on the cancel the server sends after an early throw
                if (instance == scenario.Server && failure == FailurePoint.CancelListener)
                    CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(this, (_, detail, _) =>
                    {
                        if (detail == IssueBase.IssueUpdateDetails.IssueCancel) Throw();
                    });
            });
        }

        // An early consequence throws before vanilla writes the ending, the troop return only after it
        var consequence = AccessTools.Method(typeof(IssueBase), nameof(IssueBase.AlternativeSolutionEndWithFailureConsequence));
        var errors = failure switch
        {
            FailurePoint.Consequence or FailurePoint.CancelListener => TickThreeDays(consequence),
            FailurePoint.TroopReturn => TickThreeDays(TroopReturnMethod),
            FailurePoint.ConsequenceThenTroopReturn => TickThreeDays(consequence, TroopReturnMethod),
            _ => TickThreeDays(),
        };

        Assert.Empty(errors);
        var beforeTheEnding = failure is FailurePoint.Consequence or FailurePoint.CancelListener or FailurePoint.ConsequenceThenTroopReturn;
        var troopsReturned = failure is not (FailurePoint.TroopReturn or FailurePoint.ConsequenceThenTroopReturn);
        var ending = beforeTheEnding ? IssueBase.IssueUpdateDetails.IssueCancel : IssueBase.IssueUpdateDetails.SentTroopsFailedQuest;
        Assert.Equal(ending, Assert.Single(updates));
        Assert.Equal(ending, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionJournal>()).Ending);
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        // A throw before vanilla's troop return still brings the troops home, one inside it never runs it again
        scenario.Server.Call(() => Assert.Equal(troopsReturned ? 3 : 0,
            scenario.Server.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId).MemberRoster.GetTroopCount(
                scenario.Server.GetRegisteredObject<CharacterObject>(troopId))));
        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                    entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId)));
                Assert.True(archive.IsEnded());
                Assert.Equal(ending, archive._lastIssueStatus);
                var entries = archive.GetEntries().Select(entry => entry.LogText.ToString()).ToArray();
                // A cancel closes the row as it was plus the troops returned line when they came back, an ending vanilla
                // wrote keeps the acceptance and its report once
                if (beforeTheEnding)
                    Assert.Equal(troopsReturned ? accepted[instance].Append("Your troops have returned from their mission.") : accepted[instance], entries);
                else
                {
                    Assert.All(accepted[instance], line => Assert.Contains(line, entries));
                    Assert.Single(entries, line => line.Contains("has failed to resolve the matter"));
                    Assert.Equal(entries.Length, entries.Distinct().Count());
                }
            });
        }
    }

    private static bool Throw() => throw new InvalidOperationException("Stubbed failure");

    // Patched inside the server call, which holds the game lock, so no other test runs into the throwing stubs
    private List<Exception> TickThreeDays(params MethodBase[] throwing)
    {
        var errors = new List<Exception>();
        scenario.Server.Call(() =>
        {
            var harmony = new Harmony($"alternative-solution-guard.{Guid.NewGuid():N}");
            foreach (var method in throwing)
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(AlternativeSolutionCompletionGuardTests), nameof(Throw)));
            try
            {
                CampaignEvents.OnIssueUpdatedEvent.AddNonSerializedListener(this, (_, detail, _) => updates.Add(detail));
                for (var day = 0; day < 3; day++)
                {
                    try
                    {
                        Campaign.Current.IssueManager.DailyTick();
                    }
                    catch (Exception e)
                    {
                        errors.Add(e);
                    }
                }
            }
            finally
            {
                harmony.UnpatchAll(harmony.Id);
            }
        });
        environment.FlushCoalescer();
        return errors;
    }

    private void AssertEndedOnce()
    {
        Assert.Equal(IssueBase.IssueUpdateDetails.SentTroopsFailedQuest, Assert.Single(updates));
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.Null(instance.GetRegisteredObject<Hero>(scenario.NotableId).Issue);
                Assert.False(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwner(scenario.NotableId, QuestScenario.IssueId, out _));
            });
        }
        Assert.Equal(IssueOutcome.AlternativeSolution,
            Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
        Assert.Equal(IssueBase.IssueUpdateDetails.SentTroopsFailedQuest,
            Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionJournal>()).Ending);
    }
}
