using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Packets;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;

namespace Coop.Core.Client.Services.MobileParties.Handlers
{
    /// <summary>
    /// Handles client communication related to party behavior synchronisation.
    /// </summary>
    /// <seealso cref="Server.Services.MobileParties.Handlers.ServerMobilePartyBehaviorHandler">Server's Handler</seealso>
    /// <seealso cref="GameInterface.Services.MobileParties.Handlers.MobilePartyBehaviorHandler">Game Interface's Handler</seealso>
    public class ClientMobilePartyBehaviorHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientMobilePartyBehaviorHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<ControlledPartyBehaviorUpdated>(Handle);

            // client who requests it
            messageBroker.Subscribe<ChangedWagePaymentLimit>(HandleChangedWagePaymentLimit);
            // other clients
            messageBroker.Subscribe<NetworkChangeWagePaymentLimit>(HandleChangeWageOtherClients);
        }

        private void HandleChangeWageOtherClients(MessagePayload<NetworkChangeWagePaymentLimit> payload)
        {
            var obj = payload.What;

            var message = new WagePaymentApprovedOthers(obj.MobilePartyId, obj.WageAmount);

            messageBroker.Publish(this, message);
        }


        internal void Handle(MessagePayload<ControlledPartyBehaviorUpdated> obj)
        {
            network.SendAll(new RequestMobilePartyBehaviorPacket(obj.What.BehaviorUpdateData));
        }

        private void HandleChangedWagePaymentLimit(MessagePayload<ChangedWagePaymentLimit> payload)
        {
            var obj = payload.What;
            var message = new NetworkChangeWagePaymentLimitRequest(obj.MobilePartyId, obj.WageAmount);
            network.SendAll(message);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ControlledPartyBehaviorUpdated>(Handle);

            messageBroker.Unsubscribe<ChangedWagePaymentLimit>(HandleChangedWagePaymentLimit);

            //Transpiler
            // other clients
            messageBroker.Unsubscribe<NetworkChangeWagePaymentLimit>(HandleChangeWageOtherClients);
        }
    }
}