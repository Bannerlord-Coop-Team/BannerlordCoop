using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Serilog;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Dispatch;

internal class OutcomeDispatcher : IOutcomeDispatcher
{
    private static readonly ILogger Logger = LogManager.GetLogger<OutcomeDispatcher>();

    private readonly IMessageBroker messageBroker;
    private readonly IQuestTypeRegistry registry;
    private readonly IIssueOwnerResolver ownerResolver;
    private readonly ConditionalWeakTable<IssueBase, OutcomeMarker> observedOutcomes = new();

    private sealed class OutcomeMarker
    {
    }

    private sealed class JournalMarker
    {
        public TextObject Text;
    }

    private static readonly ConditionalWeakTable<IssueBase, JournalMarker> LastJournalLogs = new();

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog), new[] { typeof(TextObject), typeof(bool) })]
    private static class QuestLogCapturePatch
    {
        [HarmonyPostfix]
        private static void Postfix(QuestBase __instance, JournalLog __result)
        {
            var issue = IssueManager.GetIssueOfQuest(__instance);
            if (issue != null && __result != null)
            {
                LastJournalLogs.Remove(issue);
                LastJournalLogs.Add(issue, new JournalMarker { Text = __result.LogText });
            }
        }
    }

    public OutcomeDispatcher(IMessageBroker messageBroker, IQuestTypeRegistry registry, IIssueOwnerResolver ownerResolver)
    {
        this.messageBroker = messageBroker;
        this.registry = registry;
        this.ownerResolver = ownerResolver;
    }

    public bool BeforeQuestOutcome(QuestBase quest, IssueOutcome outcome, out IssueBase observed)
    {
        observed = null;

        var issue = IssueManager.GetIssueOfQuest(quest);
        var registered = issue != null && registry.IsRegistered(issue.GetType());

        // A vanilla listener replayed on a client can end a quest there, only the server's ending may
        if (registered && ModInformation.IsClient && AllowedThread.IsThisThreadAllowed() && !QuestEndingReplay.IsActive)
        {
            return false;
        }

        if (CallOriginalPolicy.IsOriginalAllowed() || !registered)
        {
            return true;
        }

        if (ModInformation.IsServer)
        {
            observed = issue;
            return true;
        }

        messageBroker.Publish(quest, new QuestOutcomeRequested(quest, outcome));
        return false;
    }

    public bool BeforeIssueOutcome(IssueBase issue, IssueOutcome outcome, out IssueBase observed)
    {
        observed = null;

        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return true;
        }

        if (!registry.IsRegistered(issue.GetType()))
        {
            return true;
        }

        // A finished quest was announced by BeforeQuestOutcome, vanilla then ends its issue here through IssueManager.OnQuestCompleted
        // The server only announces an issue that ends while its quest is still running
        if (issue.IssueQuest != null)
        {
            if (ModInformation.IsServer && issue.IssueQuest.IsOngoing)
            {
                observed = issue;
            }

            return true;
        }

        if (ModInformation.IsServer)
        {
            observed = issue;
            return true;
        }

        return false;
    }

    public bool BeforeQuestBranch(QuestBase quest, byte proof)
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed())
        {
            return true;
        }

        messageBroker.Publish(quest, new QuestBranchRequested(quest, proof));
        return false;
    }

    public bool BeforeAlternativeSolutionCompletion(IssueBase issue, out IDisposable owner)
    {
        owner = null;

        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return true;
        }

        if (!registry.IsRegistered(issue.GetType()))
        {
            return true;
        }

        if (ModInformation.IsClient)
        {
            return false;
        }

        if (!ownerResolver.TryResolveOwnerHero(issue, out var ownerHero))
        {
            Logger.Error("Alternative solution of {issue} has no owning player, it is tried again tomorrow", issue.StringId);
            return false;
        }

        var scope = new MainHeroSubstitutionScope(ownerHero, ownerHero.PartyBelongedTo);

        // Vanilla tries again on the next daily tick, so the troops are not lost while the owner is busy
        if (!Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution())
        {
            scope.Dispose();
            return false;
        }

        owner = scope;
        return true;
    }

    public void After(IssueBase observed, IssueOutcome outcome)
    {
        if (observed == null || ModInformation.IsClient)
        {
            return;
        }

        if (observed.IssueOwner.Issue == observed)
        {
            return;
        }

        if (!observedOutcomes.TryGetValue(observed, out _) && outcome == IssueOutcome.QuestSuccess)
        {
            observedOutcomes.Add(observed, new OutcomeMarker());
        }
        else if (observedOutcomes.TryGetValue(observed, out _))
        {
            return;
        }

        var journalLog = observed.IssueQuest?.JournalEntries.LastOrDefault()?.LogText;
        if (journalLog == null && LastJournalLogs.TryGetValue(observed, out var marker))
        {
            journalLog = marker.Text;
        }
        messageBroker.Publish(observed, new IssueOutcomeObserved(observed, outcome, journalLog));
    }

    public void AfterAlternativeSolutionCompletion(IssueBase issue, IDisposable owner)
    {
        if (owner == null)
        {
            return;
        }

        messageBroker.Publish(issue, new IssueOutcomeObserved(issue, IssueOutcome.AlternativeSolution));
    }

    public void RunAsOwners<TQuest>(Action<TQuest> listener) where TQuest : QuestBase
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        var issues = Campaign.Current.IssueManager.Issues.Values
            .Where(issue => issue.IssueQuest is TQuest quest && quest.IsOngoing)
            .ToList();

        foreach (var issue in issues)
        {
            try
            {
                if (!ownerResolver.TryResolveOwnerHero(issue, out var ownerHero))
                {
                    Logger.Warning("{issue} has a running quest but no owning player", issue.StringId);
                    continue;
                }

                using (new MainHeroSubstitutionScope(ownerHero, ownerHero.PartyBelongedTo))
                {
                    listener((TQuest)issue.IssueQuest);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Running a {quest} listener as the owner of {issue} failed", typeof(TQuest).Name, issue.StringId);
            }
        }
    }
}
