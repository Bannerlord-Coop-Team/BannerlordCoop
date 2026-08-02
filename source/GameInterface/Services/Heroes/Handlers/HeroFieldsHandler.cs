using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Handlers
{
    /// <summary>
    /// Handler for Hero Fields
    /// </summary>
    public class HeroFieldsHandler : IHandler
    {
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
            messageBroker.Unsubscribe<ChangeHeroState>(Handle);
            messageBroker.Unsubscribe<ChangeHeroLevel>(Handle);
            messageBroker.Unsubscribe<ChangeSpcDaysInLocation>(Handle);
            messageBroker.Unsubscribe<ChangeDefaultAge>(Handle);
            messageBroker.Unsubscribe<ChangeBirthDay>(Handle);
            messageBroker.Unsubscribe<ChangePower>(Handle);
            messageBroker.Unsubscribe<ChangeCulture>(Handle);
            messageBroker.Unsubscribe<ChangeHomeSettlement>(Handle);
            messageBroker.Unsubscribe<ChangePregnant>(Handle);
        }

        /// <summary>
        /// Resolves the hero and applies the change on the game-loop thread, in queue order with the
        /// marshaled hero creation. A lookup on the network thread races a creation still waiting in
        /// the game-thread queue, permanently dropping the one-shot apply and leaving a
        /// partially-initialized hero.
        /// </summary>
        private void MarshalApply(string heroId, string context, Action<Hero> apply)
        {
            GameThread.RunSafe(() =>
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(heroId, out var instance)) return;

                apply(instance);
            }, context: context);
        }

        private void Handle(MessagePayload<ChangePregnant> payload)
        {
            var data = payload.What;

            MarshalApply(data.HeroId, nameof(ChangePregnant), instance => instance.IsPregnant = data.IsPregnant);
        }

        private void Handle(MessagePayload<ChangeHomeSettlement> payload)
        {
            var data = payload.What;

            MarshalApply(data.HeroId, nameof(ChangeHomeSettlement), instance =>
            {
                if (data.SettlementStringId == null)
                {
                    instance._homeSettlement = null;
                    return;
                }

                if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementStringId, out var settlement)) return;

                instance._homeSettlement = settlement;
            });
        }

        private void Handle(MessagePayload<ChangeCulture> payload)
        {
            var data = payload.What;

            MarshalApply(data.HeroId, nameof(ChangeCulture), instance =>
            {
                //Add CultureObject to objectManager?
                if (!objectManager.TryGetObjectWithLogging<CultureObject>(data.CultureStringId, out var culture)) return;

                instance.Culture = culture;
            });
        }

        private void Handle(MessagePayload<ChangePower> payload)
        {
            var data = payload.What;

            MarshalApply(data.HeroId, nameof(ChangePower), instance => instance._power = data.Power);
        }

        private void Handle(MessagePayload<ChangeBirthDay> payload)
        {
            var data = payload.What;

            MarshalApply(data.HeroId, nameof(ChangeBirthDay), instance => instance._birthDay = new CampaignTime(data.BirthDay));
        }

        private void Handle(MessagePayload<ChangeDefaultAge> payload)
        {
            var data = payload.What;

            MarshalApply(data.HeroId, nameof(ChangeDefaultAge), instance => instance._defaultAge = data.Age);
        }

        private void Handle(MessagePayload<ChangeSpcDaysInLocation> payload)
        {
            var data = payload.What;

            MarshalApply(data.HeroId, nameof(ChangeSpcDaysInLocation), instance =>
            {
                //instance.SpcDaysInLocation = data.Days;
            });
        }

        private void Handle(MessagePayload<ChangeHeroLevel> payload)
        {
            var data = payload.What;

            MarshalApply(data.HeroId, nameof(ChangeHeroLevel), instance => instance.Level = data.HeroLevel);
        }

        private void Handle(MessagePayload<ChangeHeroState> payload)
        {
            var data = payload.What;

            MarshalApply(data.HeroId, nameof(ChangeHeroState), instance => instance._heroState = (Hero.CharacterStates)data.HeroState);
        }

        private void Handle(MessagePayload<ChangeName> payload)
        {
            var data = payload.What;

            MarshalApply(data.HeroId, nameof(ChangeName), instance => instance._name = new TextObject(data.NewName));
        }

        private void Handle(MessagePayload<ChangeFirstName> payload)
        {
            var data = payload.What;

            MarshalApply(data.HeroId, nameof(ChangeFirstName), instance => instance._firstName = new TextObject(data.NewName));
        }

        private void Handle(MessagePayload<ChangeCharacterObject> payload)
        {
            var data = payload.What;

            MarshalApply(data.HeroId, nameof(ChangeCharacterObject), instance =>
            {
                if (!objectManager.TryGetObjectWithLogging<CharacterObject>(data.CharacterObjectId, out var character)) return;

                instance._characterObject = character;
            });
        }

        private void Handle(MessagePayload<ChangeLastTimeStamp> payload)
        {
            var data = payload.What;

            MarshalApply(data.HeroId, nameof(ChangeLastTimeStamp), instance => instance.LastTimeStampForActivity = data.LastTimeStamp);
        }
    }
}
