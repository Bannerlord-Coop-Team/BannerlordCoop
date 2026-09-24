using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.MobileParties.Packets;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Data;

namespace Coop.Core.Client.Services.MobileParties.Handlers
{
    /// <summary>
    /// Handles client communication related to party behavior synchronisation.
    /// </summary>
    /// <seealso cref="GameInterface.Services.MobileParties.Handlers.MobilePartyBehaviorHandler">Game Interface's Handler</seealso>
    public class ClientMobilePartyBehaviorHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IPartyBehaviorWireMapper wireMapper;

        public ClientMobilePartyBehaviorHandler(
            IMessageBroker messageBroker,
            INetwork network,
            IPartyBehaviorWireMapper wireMapper)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.wireMapper = wireMapper;
            messageBroker.Subscribe<ControlledPartyBehaviorUpdated>(Handle);
        }

        internal void Handle(MessagePayload<ControlledPartyBehaviorUpdated> obj)
        {
            if (!wireMapper.TryToNetwork(obj.What.BehaviorUpdateData, out var data)) return;
            network.SendAll(new RequestMobilePartyBehaviorPacket(data));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ControlledPartyBehaviorUpdated>(Handle);
        }
    }
}
