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
    /// Client handler for Ending Map Events
    /// </summary>
    public class ClientEndBattleHandler : IHandler
    {
        private readonly ILogger Logger = LogManager.GetLogger<ClientEndBattleHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientEndBattleHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<BattleEnded>(Handle);
            messageBroker.Subscribe<NetworkEndBattleApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Subscribe<BattleEnded>(Handle);
            messageBroker.Unsubscribe<NetworkEndBattleApproved>(Handle);
        }
        private void Handle(MessagePayload<BattleEnded> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkBattleEndedRequest(payload.partyId));
        }

        private void Handle(MessagePayload<NetworkEndBattleApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new EndBattle(payload.partyId));
        }
    }
}
