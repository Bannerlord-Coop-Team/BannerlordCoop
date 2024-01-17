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
    /// Server handler for Simulating Battle Rounds
    /// </summary>
    public class BattleSimulateRoundServerHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<BattleSimulateRoundServerHandler>();

        public BattleSimulateRoundServerHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<BattleRoundSimulated>(Handle);
            messageBroker.Subscribe<NetworkBattleRoundSimulatedRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<BattleRoundSimulated>(Handle);
            messageBroker.Unsubscribe<NetworkBattleRoundSimulatedRequest>(Handle);
        }
        private void Handle(MessagePayload<BattleRoundSimulated> obj)
        {
            var payload = obj.What;

            Send(payload.PartyId, payload.Side, payload.Advantage);
        }

        private void Handle(MessagePayload<NetworkBattleRoundSimulatedRequest> obj)
        {
            var payload = obj.What;

            Send(payload.PartyId, payload.Side, payload.Advantage);
        }

        private void Send(string partyId, int side, float advantage)
        {
            SimulateBattleRound simulateBattleRound = new SimulateBattleRound(partyId, side, advantage);

            messageBroker.Publish(this, simulateBattleRound);

            NetworkBattleSimulatedApproved battleSimulatedApproved = new NetworkBattleSimulatedApproved(partyId, side, advantage);

            network.SendAll(battleSimulatedApproved);
        }
    }
}