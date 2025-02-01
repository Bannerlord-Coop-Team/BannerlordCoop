using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.BasicCultureObjects.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCultureObjects.Handlers
{
    internal class BasicCultureObjectLifetimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<BasicCultureObjectLifetimeHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public BasicCultureObjectLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<BasicCultureCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateBasicCulture>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<BasicCultureCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateBasicCulture>(Handle);
        }

        private void Handle(MessagePayload<BasicCultureCreated> obj)
        {
            var payload = obj.What;

            if (objectManager.AddNewObject(payload.CultureObject, out string BasicCultureId) == false) return;

            var message = new NetworkCreateBasicCulture(BasicCultureId);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateBasicCulture> obj)
        {
            var payload = obj.What;

            var BasicCulture = ObjectHelper.SkipConstructor<BasicCultureObject>();
            if (objectManager.AddExisting(payload.CultureId, BasicCulture) == false)
            {
                Logger.Error("Failed to add existing Building, {id}", payload.CultureId);
                return;
            }
        }
    }
}