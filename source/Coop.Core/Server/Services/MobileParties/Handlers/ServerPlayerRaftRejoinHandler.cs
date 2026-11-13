using Common;
using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Rafts.Interfaces;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

internal class ServerPlayerRaftRejoinHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;
    private readonly IPlayerRaftFleetInterface raftFleetInterface;

    public ServerPlayerRaftRejoinHandler(
        IMessageBroker messageBroker,
        IPlayerManager playerManager,
        IObjectManager objectManager,
        IPlayerRaftFleetInterface raftFleetInterface)
    {
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.objectManager = objectManager;
        this.raftFleetInterface = raftFleetInterface;

        messageBroker.Subscribe<PlayerCampaignSynchronized>(Handle_PlayerCampaignSynchronized);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerCampaignSynchronized>(Handle_PlayerCampaignSynchronized);
    }

    private void Handle_PlayerCampaignSynchronized(MessagePayload<PlayerCampaignSynchronized> payload)
    {
        var peer = payload.What.PlayerId;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.TryGetPlayer(peer, out var player)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return;

            raftFleetInterface.ResumeRaft(party);
        });
    }
}
