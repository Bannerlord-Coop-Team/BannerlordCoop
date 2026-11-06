using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Common.Messaging;
using Quest = TaleWorlds.CampaignSystem.Issues.RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

internal sealed class RevenueFarmingFinalizationProofStrategy : IFinalizationProofStrategy
{
    private readonly IRevenueFarmingSessionStore sessions;
    private readonly IRevenueFarmingActions actions;

    public RevenueFarmingFinalizationProofStrategy(IRevenueFarmingSessionStore sessions, IRevenueFarmingActions actions)
    {
        this.sessions = sessions;
        this.actions = actions;
    }

    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (quest is not Quest revenue || sessions.Get(revenue) is not { } session) return false;
        var action = (RevenueFarmingAction)proof;
        if (action != RevenueFarmingAction.PayGiver && action != RevenueFarmingAction.BetrayGiver &&
            action != RevenueFarmingAction.PayDemand && action != RevenueFarmingAction.BetrayDemand &&
            action != RevenueFarmingAction.PaySteward) return false;
        if (actions.Execute(session, new RequestRevenueFarmingAction(null, quest.StringId, session.Revision, action))) return true;
        if (session.AwaitingPayment && quest.QuestDueTime.IsPast && session.PlayerHero.Gold < revenue._totalRequestedDenars)
        {
            using (new RevenueFarmingExecutionScope(session)) quest.CompleteQuestWithTimeOut();
        }
        else
        {
            MessageBroker.Instance.Publish(this, new RevenueFarmingChanged(session, refreshPresentation: true));
        }
        return true;
    }
}
