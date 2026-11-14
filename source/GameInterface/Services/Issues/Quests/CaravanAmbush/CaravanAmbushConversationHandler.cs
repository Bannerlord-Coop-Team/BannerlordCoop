using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.PlayerCaptivityService.Messages;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal class CaravanAmbushConversationHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IIssueResolver issues;
    private readonly IControllerIdProvider controller;
    private readonly IIssueConversationGuard conversationGuard;
    private NetworkCaravanAmbushConversation? pendingConversation;

    public CaravanAmbushConversationHandler(IMessageBroker broker, IIssueResolver issues,
        IControllerIdProvider controller, IIssueConversationGuard conversationGuard)
    {
        this.broker = broker;
        this.issues = issues;
        this.controller = controller;
        this.conversationGuard = conversationGuard;
        broker.Subscribe<NetworkCaravanAmbushConversation>(Conversation);
        broker.Subscribe<CampaignTick>(Tick);
    }

    public void Dispose()
    {
        broker.Unsubscribe<NetworkCaravanAmbushConversation>(Conversation);
        broker.Unsubscribe<CampaignTick>(Tick);
        pendingConversation = null;
    }

    private void Conversation(MessagePayload<NetworkCaravanAmbushConversation> payload)
    {
        if (ModInformation.IsServer || payload.What.ControllerId != controller.ControllerId) return;
        var data = payload.What;
        GameThread.RunSafe(() => conversationGuard.RunInOrder(() =>
        {
            pendingConversation = data;
            TryOpenConversation();
        }, data.IssueOwnerId), context: nameof(CaravanAmbushConversationHandler));
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
        // These cases discard the pending conversation.
        if (!TryResolvePending(data, out var quest, out var caravan))
        {
            pendingConversation = null;
            return;
        }
        // The remaining guards keep it pending until a later tick.
        if (!quest._isCaravanSaved) return;
        if (!IsOwnerFreeOnMap()) return;
        var leader = ConversationHelper.GetConversationCharacterPartyLeader(caravan.Party);
        if (leader == null) return;
        CampaignMapConversation.OpenConversation(
            new ConversationCharacterData(Hero.MainHero.CharacterObject),
            new ConversationCharacterData(leader));
        pendingConversation = null;
    }

    private bool TryResolvePending(NetworkCaravanAmbushConversation data, out Quest quest, out MobileParty caravan)
    {
        quest = null;
        caravan = null;
        if (!issues.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
            !(issue.IssueQuest is Quest resolved) || !resolved.IsOngoing) return false;
        quest = resolved;
        caravan = quest._caravanParty;
        return !quest._isCaravanSaved || caravan?.IsActive == true;
    }

    private bool IsOwnerFreeOnMap() => GameStateManager.Current?.ActiveState is MapState map && !map.AtMenu &&
        !map.IsSimulationActive && !Campaign.Current.ConversationManager.IsConversationInProgress;
}
