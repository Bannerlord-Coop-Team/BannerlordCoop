using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
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

    [Fact]
    public void IssueThatCannotBeDetachedIsNeverRunAgainAndEndsOnceItCanBe()
    {
        // Both IssueFinalized and the deactivation fallback go through DeactivateIssue, so the issue stays attached
        var errors = TickThreeDays(TroopReturnMethod, AccessTools.Method(typeof(IssueManager), nameof(IssueManager.DeactivateIssue)));

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

    public enum FailurePoint { Consequence, EndingListener, TroopReturn }

    // An early consequence throws before vanilla writes the ending, the troop return only after it
    [Theory]
    [InlineData(FailurePoint.Consequence, IssueBase.IssueUpdateDetails.IssueCancel, 3, "Your troops have returned from their mission.")]
    [InlineData(FailurePoint.EndingListener, IssueBase.IssueUpdateDetails.SentTroopsFailedQuest, 3, "has failed to resolve the matter")]
    [InlineData(FailurePoint.TroopReturn, IssueBase.IssueUpdateDetails.SentTroopsFailedQuest, 0, "has failed to resolve the matter")]
    public void ThrowAfterAcceptanceLeavesTheOwnersJournalEndedOnce(FailurePoint failure, IssueBase.IssueUpdateDetails ending,
        int troopsBack, string lastLine)
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
                        if (detail == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest) ThrowingMethodsScope.Throw();
                    });
            });
        }

        var errors = failure switch
        {
            FailurePoint.Consequence => TickThreeDays(AccessTools.Method(typeof(IssueBase), nameof(IssueBase.AlternativeSolutionEndWithFailureConsequence))),
            FailurePoint.TroopReturn => TickThreeDays(TroopReturnMethod),
            _ => TickThreeDays(),
        };

        Assert.Empty(errors);
        Assert.Equal(ending, Assert.Single(updates));
        Assert.Equal(ending, Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionJournal>()).Ending);
        Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        // A throw before vanilla's troop return still brings the troops home, one inside it never runs it again
        scenario.Server.Call(() => Assert.Equal(troopsBack,
            scenario.Server.GetRegisteredObject<MobileParty>(scenario.OwnerPartyId).MemberRoster.GetTroopCount(
                scenario.Server.GetRegisteredObject<CharacterObject>(troopId))));
        // The owner's row keeps the acceptance and gains the ending's last line once
        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                    entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId)));
                Assert.True(archive.IsEnded());
                Assert.Equal(ending, archive._lastIssueStatus);
                var entries = archive.GetEntries().Select(entry => entry.LogText.ToString()).ToArray();
                Assert.Equal(accepted[instance], entries.Take(accepted[instance].Length));
                Assert.Contains(lastLine, entries.Last());
                Assert.Single(entries, line => line.Contains(lastLine));
                Assert.Equal(entries.Length, entries.Distinct().Count());
            });
        }
    }

    private List<Exception> TickThreeDays(params MethodBase[] throwing)
    {
        var errors = new List<Exception>();
        scenario.Server.Call(() =>
        {
            using var throwingMethods = new ThrowingMethodsScope(throwing);
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
