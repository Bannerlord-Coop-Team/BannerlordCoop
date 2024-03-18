using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Handlers
{
    /// <summary>
    /// Handler for Hero Fields
    /// </summary>
    public class HeroFieldsHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroFieldsHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        public HeroFieldsHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;

            messageBroker.Subscribe<ChangeLastTimeStamp>(Handle);
            messageBroker.Subscribe<ChangeCharacterObject>(Handle);
            messageBroker.Subscribe<ChangeFirstName>(Handle);
            messageBroker.Subscribe<ChangeName>(Handle);
            messageBroker.Subscribe<ChangeHairTags>(Handle);
        }

        private void Handle(MessagePayload<ChangeHairTags> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
            }
            instance.HairTags = data.HairTags;
        }

        private void Handle(MessagePayload<ChangeName> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
            }
            instance._name = new TextObject(data.NewName);
        }

        private void Handle(MessagePayload<ChangeFirstName> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
            }
            instance._firstName = new TextObject(data.NewName);
        }
        private void Handle(MessagePayload<ChangeCharacterObject> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
            }
            if (objectManager.TryGetObject<CharacterObject>(data.CharacterObjectId, out var character) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(CharacterObject), data.CharacterObjectId);
            }

            instance._characterObject = character;
        }
        private void Handle(MessagePayload<ChangeLastTimeStamp> payload)
        {
            var data = payload.What;
            if(objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
            }

            instance.LastTimeStampForActivity = data.LastTimeStamp;
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<ChangeLastTimeStamp>(Handle);
            messageBroker.Unsubscribe<ChangeCharacterObject>(Handle);
            messageBroker.Unsubscribe<ChangeFirstName>(Handle);
            messageBroker.Unsubscribe<ChangeName>(Handle);
        }
    }
}