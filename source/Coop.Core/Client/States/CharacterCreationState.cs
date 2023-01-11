using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State controller for the character creation client state
    /// </summary>
    public class CharacterCreationState : ClientStateBase
    {
        public CharacterCreationState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<MainMenuEntered>(Handle);
            MessageBroker.Subscribe<CharacterCreationFinished>(Handle);
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic, MessageBroker);
        }

        private void Handle(MessagePayload<CharacterCreationFinished> obj)
        {
            Logic.State = new ReceivingSavedDataState(Logic, MessageBroker);
            MessageBroker.Publish(this, new LoadGameSave());
        }

        public override void EnterMainMenu()
        {
            MessageBroker.Publish(this, new EnterMainMenu());
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<MainMenuEntered>(Handle);
            MessageBroker.Unsubscribe<CharacterCreationFinished>(Handle);
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
            MessageBroker.Publish(this, new EnterMainMenu());
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
