using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Clans.Messages;
using Coop.Core.Server.Services.Bandits.Messages;
using Coop.Core.Server.Services.Clans.Messages;
using GameInterface.Services.Bandits.Messages;
using GameInterface.Services.Clans.Messages;
using Serilog;

namespace Coop.Core.Client.Services.Bandits.Handler
{
    /// <summary>
    /// Handles all bandit spawning.
    /// </summary>
    public class ClientBanditsSpawnHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ClientBanditsSpawnHandler>();

        public ClientBanditsSpawnHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkBanditSpawnApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkBanditSpawnApproved>(Handle);
        }

        private void Handle(MessagePayload<NetworkBanditSpawnApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new SpawnBandits(payload.ClanId));
        }
    }
}