using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

internal class OutcomeDispatcher : IOutcomeDispatcher
{
    private static readonly ILogger Logger = LogManager.GetLogger<OutcomeDispatcher>();

    private readonly IMessageBroker messageBroker;
    private readonly IQuestTypeRegistry registry;
    private readonly IIssueOwnerResolver ownerResolver;

    public OutcomeDispatcher(IMessageBroker messageBroker, IQuestTypeRegistry registry, IIssueOwnerResolver ownerResolver)
    {
        this.messageBroker = messageBroker;
        this.registry = registry;
        this.ownerResolver = ownerResolver;
    }

    public bool BeforeQuestOutcome(QuestBase quest, IssueOutcome outcome, out IssueBase observed)
    {
        observed = null;

        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return true;
        }

        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !registry.IsRegistered(issue.GetType()))
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

        // An issue with a quest ends as a consequence of the quest outcome, which is handled on its own
        if (issue.IssueQuest != null)
        {
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
}
