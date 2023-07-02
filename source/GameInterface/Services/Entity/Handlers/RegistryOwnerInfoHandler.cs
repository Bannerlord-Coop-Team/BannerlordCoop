using Common.Messaging;
using GameInterface.Services.Entity.Messages;

namespace GameInterface.Services.Entity.Handlers
{
    internal class RegistryOwnerInfoHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IControlledEntityRegistry controlledEntityRegistry;

        public RegistryOwnerInfoHandler(
            IMessageBroker messageBroker, 
            IControlledEntityRegistry controlledEntityRegistry) 
        {
            this.messageBroker = messageBroker;
            this.controlledEntityRegistry = controlledEntityRegistry;

            messageBroker.Subscribe<SetRegistryOwnerId>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SetRegistryOwnerId>(Handle);
        }

        private void Handle(MessagePayload<SetRegistryOwnerId> obj)
        {
            controlledEntityRegistry.InstanceOwnerId = obj.What.OwnerId;
        }
    }
}
