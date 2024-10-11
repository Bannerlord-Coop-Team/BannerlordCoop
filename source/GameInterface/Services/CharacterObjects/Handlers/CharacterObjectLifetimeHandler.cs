using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.CharacterObjects.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Buildings.Handlers
{
    internal class CharacterObjectLifetimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CharacterObjectLifetimeHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public CharacterObjectLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<CharacterObjectCreated>(Handle_CharacterCreated);
            messageBroker.Subscribe<NetworkCreateCharacterObject>(Handle_CreateCharacter);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<CharacterObjectCreated>(Handle_CharacterCreated);
            messageBroker.Unsubscribe<NetworkCreateCharacterObject>(Handle_CreateCharacter);
        }

        private void Handle_CharacterCreated(MessagePayload<CharacterObjectCreated> obj)
        {
            var payload = obj.What;

            if (objectManager.AddNewObject(payload.CharacterObject, out string characterObjectId) == false) return;

            var message = new NetworkCreateCharacterObject(characterObjectId);
            network.SendAll(message);
        }

        private void Handle_CreateCharacter(MessagePayload<NetworkCreateCharacterObject> obj)
        {
            var payload = obj.What;

            var characterObject = ObjectHelper.SkipConstructor<CharacterObject>();
            if (objectManager.AddExisting(payload.CharacterObjectId, characterObject) == false)
            {
                Logger.Error("Failed to add existing Building, {id}", payload.CharacterObjectId);
                return;
            }
        }
    }
}
