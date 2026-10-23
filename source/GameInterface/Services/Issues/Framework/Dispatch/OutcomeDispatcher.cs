using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly Dictionary<QuestBase, QuestOutcomeObservation> questOutcomes =
        new Dictionary<QuestBase, QuestOutcomeObservation>(ReferenceComparer<QuestBase>.Instance);

    /// <summary>Only retained while this quest's completion calls are on the stack.</summary>
    private sealed class QuestOutcomeObservation
    {
        public int Depth;
        public IssueOutcome? Outcome;
    }

    public OutcomeDispatcher(IMessageBroker messageBroker, IQuestTypeRegistry registry, IIssueOwnerResolver ownerResolver)
    {
        this.messageBroker = messageBroker;
        this.registry = registry;
        this.ownerResolver = ownerResolver;
    }

    public bool BeforeQuestOutcome(QuestBase quest, IssueOutcome outcome, out IssueBase observed, TextObject log = null)
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
            if (!questOutcomes.TryGetValue(quest, out var observation))
            {
                observation = new QuestOutcomeObservation();
                questOutcomes.Add(quest, observation);
            }
            observation.Depth++;
            return true;
        }

        messageBroker.Publish(quest, new QuestOutcomeRequested(quest, outcome, log));
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

        // The outer quest dispatch announces quest completion; only an independently ending issue is observed here.
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

    public void ObserveQuestCompletion(QuestBase quest, QuestBase.QuestCompleteDetails detail)
    {
        if (ModInformation.IsClient || !questOutcomes.TryGetValue(quest, out var observation)) return;

        observation.Outcome = detail switch
        {
            QuestBase.QuestCompleteDetails.Success => IssueOutcome.QuestSuccess,
            QuestBase.QuestCompleteDetails.Fail => IssueOutcome.QuestFail,
            QuestBase.QuestCompleteDetails.Cancel => IssueOutcome.QuestCancel,
            QuestBase.QuestCompleteDetails.Timeout => IssueOutcome.QuestTimeOut,
            QuestBase.QuestCompleteDetails.FailWithBetrayal => IssueOutcome.QuestBetrayal,
            _ => (IssueOutcome?)null,
        };
    }

    public void AfterQuestOutcome(QuestBase quest, IssueBase observed, Exception exception)
    {
        if (observed == null || ModInformation.IsClient) return;

        var observation = questOutcomes[quest];
        if (--observation.Depth != 0) return;
        questOutcomes.Remove(quest);

        // A nested failure can return to a timeout, while OnBeforeTimedOut can redirect to success or defer.
        if (exception == null && observation.Outcome.HasValue)
            After(observed, observation.Outcome.Value);
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

        messageBroker.Publish(observed, new IssueOutcomeObserved(observed, outcome));
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
