using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

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
            messageBroker.Subscribe<ChangeBeardTags>(Handle);
            messageBroker.Subscribe<ChangeTattooTags>(Handle);
            messageBroker.Subscribe<ChangeHeroState>(Handle);
            messageBroker.Subscribe<ChangeHeroLevel>(Handle);
            messageBroker.Subscribe<ChangeSpcDaysInLocation>(Handle);
            messageBroker.Subscribe<ChangeDefaultAge>(Handle);
            messageBroker.Subscribe<ChangeBirthDay>(Handle);
            messageBroker.Subscribe<ChangePower>(Handle);
            messageBroker.Subscribe<ChangeCulture>(Handle);
            messageBroker.Subscribe<ChangeHomeSettlement>(Handle);
            messageBroker.Subscribe<ChangePregnant>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ChangeLastTimeStamp>(Handle);
            messageBroker.Unsubscribe<ChangeCharacterObject>(Handle);
            messageBroker.Unsubscribe<ChangeFirstName>(Handle);
            messageBroker.Unsubscribe<ChangeName>(Handle);
            messageBroker.Unsubscribe<ChangeHairTags>(Handle);
            messageBroker.Unsubscribe<ChangeBeardTags>(Handle);
            messageBroker.Unsubscribe<ChangeTattooTags>(Handle);
            messageBroker.Unsubscribe<ChangeHeroState>(Handle);
            messageBroker.Unsubscribe<ChangeHeroLevel>(Handle);
            messageBroker.Unsubscribe<ChangeSpcDaysInLocation>(Handle);
            messageBroker.Unsubscribe<ChangeDefaultAge>(Handle);
            messageBroker.Unsubscribe<ChangeBirthDay>(Handle);
            messageBroker.Unsubscribe<ChangePower>(Handle);
            messageBroker.Unsubscribe<ChangeCulture>(Handle);
            messageBroker.Unsubscribe<ChangePregnant>(Handle);
        }

        private void Handle(MessagePayload<ChangePregnant> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            instance.IsPregnant = data.IsPregnant;
        }

        private void Handle(MessagePayload<ChangeHomeSettlement> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }

            if (data.SettlementStringId == null)
            {
                instance._homeSettlement = null;
                return;
            }

            if (objectManager.TryGetObject<Settlement>(data.SettlementStringId, out var settlement) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Settlement), data.SettlementStringId);
                return;
            }
            instance._homeSettlement = settlement;
        }

        private void Handle(MessagePayload<ChangeCulture> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            //Add CultureObject to objectManager?
            if (objectManager.TryGetObject<CultureObject>(data.CultureStringId, out var culture) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            instance.Culture = culture;
        }

        private void Handle(MessagePayload<ChangePower> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            instance._power = data.Power;
        }

        private void Handle(MessagePayload<ChangeBirthDay> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            instance._birthDay = new CampaignTime(data.BirthDay);
        }

        private void Handle(MessagePayload<ChangeDefaultAge> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            instance._defaultAge = data.Age;
        }

        private void Handle(MessagePayload<ChangeSpcDaysInLocation> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            instance.SpcDaysInLocation = data.Days;
        }

        private void Handle(MessagePayload<ChangeHeroLevel> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            instance.Level = data.HeroLevel;
        }

        private void Handle(MessagePayload<ChangeHeroState> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            instance._heroState = (Hero.CharacterStates)data.HeroState;
        }
        private void Handle(MessagePayload<ChangeTattooTags> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            instance.TattooTags = data.TattooTags;
        }

        private void Handle(MessagePayload<ChangeBeardTags> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            instance.BeardTags = data.BeardTags;
        }

        private void Handle(MessagePayload<ChangeHairTags> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            instance.HairTags = data.HairTags;
        }

        private void Handle(MessagePayload<ChangeName> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            instance._name = new TextObject(data.NewName);
        }

        private void Handle(MessagePayload<ChangeFirstName> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            instance._firstName = new TextObject(data.NewName);
        }
        private void Handle(MessagePayload<ChangeCharacterObject> payload)
        {
            var data = payload.What;
            if (objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }
            if (objectManager.TryGetObject<CharacterObject>(data.CharacterObjectId, out var character) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(CharacterObject), data.CharacterObjectId);
                return;
            }

            instance._characterObject = character;
        }
        private void Handle(MessagePayload<ChangeLastTimeStamp> payload)
        {
            var data = payload.What;
            if(objectManager.TryGetObject<Hero>(data.HeroId, out var instance) == false)
            {
                Logger.Error("Unable to find {type} with id: {id}", typeof(Hero), data.HeroId);
                return;
            }

            instance.LastTimeStampForActivity = data.LastTimeStamp;
        }
    }
}