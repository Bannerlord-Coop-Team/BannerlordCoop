using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.MobileParties.Packets;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;

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
        private readonly IObjectManager objectManager;

        public ClientMobilePartyBehaviorHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<ControlledPartyBehaviorUpdated>(Handle);
        }

        internal void Handle(MessagePayload<ControlledPartyBehaviorUpdated> obj)
        {
            network.SendAll(new RequestMobilePartyBehaviorPacket(obj.What.BehaviorUpdateData));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ControlledPartyBehaviorUpdated>(Handle);
        }
    }
}