using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States
{
    public class MainMenuState : ClientStateBase
    {
        public MainMenuState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<Connected>(Handle);
        }

        private void Handle(MessagePayload<Connected> obj)
        {
            if (obj.What.ClientPartyExists)
            {
                Logic.State = new ReceivingSavedDataState(Logic, MessageBroker);
                MessageBroker.Publish(this, new LoadGameSave());
            }
            else
            {
                Logic.State = new CharacterCreationState(Logic, MessageBroker);
                MessageBroker.Publish(this, new StartCreateCharacter());
            }
        }

        public override void Connect()
        {
            MessageBroker.Publish(this, new Connect());
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<Connected>(Handle);
        }

        public override void Disconnect()
        {
        }

        public override void EnterMainMenu()
        {
        }

        public override void ExitGame()
        {
        }

        public override void LoadSavedData()
        {
        }

        public override void StartCharacterCreation()
        {
        }

        public override void EnterCampaignState()
        {
        }

        public override void EnterMissionState()
        {
        }

        public override void ResolveNetworkGuids()
        {
        }
    }
}
