using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.GameDebug.Messages;
using LiteNetLib;
using Serilog;
using System.Linq;

namespace Coop.Core.Common.Services.Connection.Handlers
{
    public class PlayerConnectionServerMessageHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<PlayerConnectionServerMessageHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IClientRegistry clientRegistry;
        private readonly INetwork network;

        public PlayerConnectionServerMessageHandler(IMessageBroker messageBroker, IClientRegistry clientRegistry, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.clientRegistry = clientRegistry;
            this.network = network;

            messageBroker.Subscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
            messageBroker.Subscribe<PlayerConnected>(PlayerConnectedHandler);
            messageBroker.Subscribe<NetworkRequestTimeSpeedChange>(NetworkRequestTimeSpeedChange);
            messageBroker.Subscribe<NetworkConnected>(Handle_NetworkConnected);
        }

        internal void Handle_NetworkConnected(MessagePayload<NetworkConnected> obj)
        {
            messageBroker.Publish(this, new SendInformationMessage("A new player is joining the game, pausing"));
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
        private void PlayerConnectedHandler(MessagePayload<PlayerConnected> obj)
        {
            messageBroker.Publish(this, new SendInformationMessage("A new player is joining the game, pausing"));
        }
        private void NetworkRequestTimeSpeedChange(MessagePayload<NetworkRequestTimeSpeedChange> obj)
        {
            if (AnyLoaders())
            {
                int loadingPeers = clientRegistry.LoadingPeers.Count;
                messageBroker.Publish(this, new SendInformationMessage("Pausing disabled, " + loadingPeers + " player(s) are currently joining the game"));
                network.SendAll(new SendInformationMessage("Pausing disabled, " + loadingPeers + " player(s) are currently joining the game"));
            }
        }

        private bool AnyLoaders()
        {
            if (clientRegistry.PlayersLoading)
            {
                var loadingPeers = clientRegistry.LoadingPeers;
                Logger.Information($"{string.Join(",", loadingPeers.Select(p => p.EndPoint.ToString()))} are currently loading, unable to change time");
                return true;
            }

            return false;
        }
    }
}
