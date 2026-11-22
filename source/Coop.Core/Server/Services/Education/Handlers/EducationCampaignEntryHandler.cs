using Common;
using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Education.Interfaces;
using GameInterface.Services.Players;

namespace Coop.Core.Server.Services.Education.Handlers;

/// <summary>
/// Education notifications sent while a client is still loading are lost, so a player
/// can only be picked as an educator once it enters the campaign.
/// </summary>
internal class EducationCampaignEntryHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IEducationCampaignBehaviorInterface educationInterface;

    public EducationCampaignEntryHandler(
        IMessageBroker messageBroker,
        IPlayerManager playerManager,
        IEducationCampaignBehaviorInterface educationInterface)
    {
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.educationInterface = educationInterface;

        messageBroker.Subscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    private void Handle_PlayerCampaignEntered(MessagePayload<PlayerCampaignEntered> payload)
    {
        if (ModInformation.IsClient) return;

        if (!playerManager.TryGetPlayer(payload.What.playerId, out var player)) return;

        educationInterface.SetPlayerInCampaign(player.ControllerId);
    }
}
