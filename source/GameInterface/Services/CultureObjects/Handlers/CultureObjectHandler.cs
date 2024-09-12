using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.BasicCultureObjects.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.BasicCultureObjects.Handlers
{
    internal class CultureObjectHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<CultureObjectHandler>();

        public CultureObjectHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<CultureObjectCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateCultureObject>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<CultureObjectCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateCultureObject>(Handle);
        }

        private void Handle(MessagePayload<CultureObjectCreated> payload)
        {
            objectManager.AddNewObject(payload.What.CultureObject, out string newId);
            NetworkCreateCultureObject message = new(newId);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateCultureObject> obj)
        {
            var newCultureObject = ObjectHelper.SkipConstructor<CultureObject>();

            var payload = obj.What;

            objectManager.AddExisting(payload.CultureObjectId, newCultureObject);
        }
    }
}
