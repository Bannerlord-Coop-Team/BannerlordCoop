using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.CharacterSkills.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.Core;

namespace GameInterface.Services.CharacterSkills.Handlers
{
    internal class CharacterSkillsLifetimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CharacterSkillsLifetimeHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public CharacterSkillsLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<CharacterSkillsCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateCharacterSkills>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<CharacterSkillsCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateCharacterSkills>(Handle);
        }

        private void Handle(MessagePayload<CharacterSkillsCreated> obj)
        {
            var payload = obj.What;

            if (objectManager.AddNewObject(payload.CharacterSkills, out string CharacterSkillsId) == false) return;

            var message = new NetworkCreateCharacterSkills(CharacterSkillsId);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateCharacterSkills> obj)
        {
            var payload = obj.What;

            var CharacterSkills = ObjectHelper.SkipConstructor<MBCharacterSkills>();
            if (objectManager.AddExisting(payload.CharacterSkillsId, CharacterSkills) == false)
            {
                Logger.Error("Failed to add existing CharacterSkill, {id}", payload.CharacterSkillsId);
                return;
            }
        }
    }
}