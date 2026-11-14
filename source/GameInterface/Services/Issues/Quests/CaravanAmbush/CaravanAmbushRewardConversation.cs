using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.PlayerCaptivityService.Messages;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal interface ICaravanAmbushRewardConversation
{
    void Request(string issueOwnerId, string issueId);
}

internal class CaravanAmbushRewardConversation : ICaravanAmbushRewardConversation, IHandler
{
    private readonly IMessageBroker broker;
    private readonly IIssueResolver issues;
    private readonly ICaravanAmbushConversationMap map;
    private (string IssueOwnerId, string IssueId)? pendingConversation;

    public CaravanAmbushRewardConversation(IMessageBroker broker, IIssueResolver issues, ICaravanAmbushConversationMap map)
    {
        this.broker = broker;
        this.issues = issues;
        this.map = map;
        broker.Subscribe<CampaignTick>(Tick);
    }

    public void Dispose()
    {
        broker.Unsubscribe<CampaignTick>(Tick);
        pendingConversation = null;
    }

    public void Request(string issueOwnerId, string issueId)
    {
        pendingConversation = (issueOwnerId, issueId);
        TryOpenConversation();
    }

    private void Tick(MessagePayload<CampaignTick> payload)
    {
        // Vanilla TickEvent stops while paused, including after battle loot closes.
        if (ModInformation.IsClient) TryOpenConversation();
    }

    private void TryOpenConversation()
    {
        if (!pendingConversation.HasValue) return;
        var data = pendingConversation.Value;
        if (!issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
            !(issue.IssueQuest is Quest quest) || !quest.IsOngoing)
        {
            pendingConversation = null;
            return;
        }
        if (!quest._isCaravanSaved || quest._caravanParty == null || !quest._caravanParty.IsActive) return;
        if (!map.IsOwnerFreeOnMap()) return;
        pendingConversation = null;
        if (!map.TryOpen(quest._caravanParty)) pendingConversation = data;
    }
}
