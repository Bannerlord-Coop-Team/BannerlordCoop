using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.BasicCharacterObjects.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCharacterObjects.Handlers
{
    internal class BasicCharacterLifetimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<BasicCharacterLifetimeHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public BasicCharacterLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<BasicCharacterCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateBasicCharacter>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<BasicCharacterCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateBasicCharacter>(Handle);
        }

        private void Handle(MessagePayload<BasicCharacterCreated> obj)
        {
            var payload = obj.What;

            if (objectManager.AddNewObject(payload.CharacterObject, out string basicCharacterId) == false) return;

            var message = new NetworkCreateBasicCharacter(basicCharacterId);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateBasicCharacter> obj)
        {
            var payload = obj.What;

            var basicCharacter = ObjectHelper.SkipConstructor<BasicCharacterObject>();
            if (objectManager.AddExisting(payload.BasicCharacterId, basicCharacter) == false)
            {
                Logger.Error("Failed to add existing Building, {id}", payload.BasicCharacterId);
                return;
            }
        }
    }
}
