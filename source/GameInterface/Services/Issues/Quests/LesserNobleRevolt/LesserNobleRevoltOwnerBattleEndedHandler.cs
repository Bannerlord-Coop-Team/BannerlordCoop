using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

internal sealed class LesserNobleRevoltOwnerBattleEndedHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IIssueResolver issueResolver;
    private readonly IControllerIdProvider controllerIdProvider;

    public LesserNobleRevoltOwnerBattleEndedHandler(
        IMessageBroker messageBroker,
        IIssueResolver issueResolver,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.issueResolver = issueResolver;
        this.controllerIdProvider = controllerIdProvider;
        messageBroker.Subscribe<NetworkLesserNobleRevoltOwnerBattleEnded>(Handle);
    }

    public void Dispose() => messageBroker.Unsubscribe<NetworkLesserNobleRevoltOwnerBattleEnded>(Handle);

    private void Handle(MessagePayload<NetworkLesserNobleRevoltOwnerBattleEnded> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controllerIdProvider.ControllerId)
        {
            return;
        }

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!issueResolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest) || !quest.IsOngoing)
            {
                return;
            }

            // What vanilla's OnMapEventEnd sets, the client never sees the server's battle end.
            quest._firstTalkIsDone = true;
            quest._checkForEventEnd = false;
        }, context: nameof(LesserNobleRevoltOwnerBattleEndedHandler));
    }
}
