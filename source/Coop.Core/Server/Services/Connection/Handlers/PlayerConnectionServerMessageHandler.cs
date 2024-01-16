using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using Serilog;
using System.Linq;

namespace Coop.Core.Server.Services.Connection.Handlers
{
    public class PlayerConnectionServerMessageHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<PlayerConnectionServerMessageHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IClientRegistry clientRegistry;
        private readonly INetwork network;

        private string unpauseReadyMessage = "All players connected, game can now be un-paused";

        public PlayerConnectionServerMessageHandler(IMessageBroker messageBroker, IClientRegistry clientRegistry, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.clientRegistry = clientRegistry;
            this.network = network;

            messageBroker.Subscribe<PlayerCampaignEntered>(PlayerCampaignEnteredHandler);
            messageBroker.Subscribe<AttemptedTimeSpeedChanged>(AttemptedTimeSpeedChanged);
            messageBroker.Subscribe<PackageGameSaveData>(Handle_NetworkConnected);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PlayerCampaignEntered>(PlayerCampaignEnteredHandler);
            messageBroker.Unsubscribe<AttemptedTimeSpeedChanged>(AttemptedTimeSpeedChanged);
            messageBroker.Unsubscribe<PackageGameSaveData>(Handle_NetworkConnected);
        }

        internal void Handle_NetworkConnected(MessagePayload<PackageGameSaveData> obj)
        {
            messageBroker.Publish(this, new SendInformationMessage("A new player is joining the game, pausing"));
        }

        private void PlayerCampaignEnteredHandler(MessagePayload<PlayerCampaignEntered> obj)
        {
            var playerId = obj.What.playerId;

            if (!AnyLoaders())
            {
                messageBroker.Publish(this, new SendInformationMessage(unpauseReadyMessage));
                network.SendAllBut(playerId, new SendInformationMessage(unpauseReadyMessage));
            }
        }

        private void AttemptedTimeSpeedChanged(MessagePayload<AttemptedTimeSpeedChanged> obj)
        {
            if (AnyLoaders())
            {
                int loadingPeers = clientRegistry.LoadingPeers.Count;

                string loadingMessage = "Pausing disabled, " + loadingPeers + " player(s) are currently joining the game";

                messageBroker.Publish(this, new SendInformationMessage(loadingMessage));
                network.SendAll(new SendInformationMessage(loadingMessage));
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
