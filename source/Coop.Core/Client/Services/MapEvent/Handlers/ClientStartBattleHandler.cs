using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MapEvent.Messages;
using Coop.Core.Server.Services.MapEvent.Messages;
using GameInterface.Services.MapEvents.Messages;
using Serilog;

namespace Coop.Core.Client.Services.MapEvent.Handlers
{
    /// <summary>
    /// Client handler for Starting Map Events
    /// </summary>
    public class ClientStartBattleHandler : IHandler
    {
        private readonly ILogger Logger = LogManager.GetLogger<ClientStartBattleHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientStartBattleHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<BattleStarted>(Handle);
            messageBroker.Subscribe<NetworkStartBattleApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<BattleStarted>(Handle);
            messageBroker.Unsubscribe<NetworkStartBattleApproved>(Handle);
        }
        private void Handle(MessagePayload<BattleStarted> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkBattleStartedRequest(
                payload.attackerPartyId, 
                payload.defenderPartyId));
        }
        private void Handle(MessagePayload<NetworkStartBattleApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new StartBattle(
                payload.attackerPartyId,
                payload.defenderPartyId));
        }
    }
}
