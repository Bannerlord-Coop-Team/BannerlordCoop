using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameDebug.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Services.Players.Handlers
{
    public class PlayerConnectionServerMessageHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IClientRegistry clientRegistry;
        private readonly INetwork network;

        public PlayerConnectionServerMessageHandler(IMessageBroker messageBroker, IClientRegistry clientRegistry, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.clientRegistry = clientRegistry;
            this.network = network;

            messageBroker.Subscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
        }

        private void PlayerCampaignEnteredHandler(MessagePayload<NetworkPlayerCampaignEntered> obj)
        {
            var playerId = (NetPeer)obj.Who;

            if (!clientRegistry.PlayersLoading)
            {
                messageBroker.Publish(this, new SendInformationMessage("All players connected, game can now be un-paused"));
                network.SendAllBut(playerId, new SendInformationMessage("All players connected, game can now be un-paused"));
            }
        }
    }
}
