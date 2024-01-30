using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MapEvent.Messages;
using Coop.Core.Server.Services.MapEvent.Messages;
using GameInterface.Services.MapEvents.Messages;
using Serilog;

namespace Coop.Core.Server.Services.MapEvent.Handlers
{
    /// <summary>
    /// Server handler for Ending Map Events
    /// </summary>
    public class ServerEndBattleHandler : IHandler
    {
        private readonly ILogger Logger = LogManager.GetLogger<ServerEndBattleHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerEndBattleHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<BattleEnded>(Handle);
            messageBroker.Subscribe<NetworkBattleEndedRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<BattleEnded>(Handle);
            messageBroker.Unsubscribe<NetworkBattleEndedRequest>(Handle);
        }
        private void Handle(MessagePayload<BattleEnded> obj)
        {
            var payload = obj.What;

            Send(payload.partyId);
        }

        private void Handle(MessagePayload<NetworkBattleEndedRequest> obj)
        {
            var payload = obj.What;

            Send(payload.partyId);
        }

        private void Send(string partyId)
        {
            EndBattle endBattle = new EndBattle(partyId);

            messageBroker.Publish(this, endBattle);

            NetworkEndBattleApproved endBattleApproved = new NetworkEndBattleApproved(partyId);

            network.SendAll(endBattleApproved);
        }
    }
}