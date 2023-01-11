using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State Logic Controller for the Receiving Saved Data State
    /// </summary>
    public class ReceivingSavedDataState : ClientStateBase
    {
        public ReceivingSavedDataState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<MainMenuEntered>(Handle);
            MessageBroker.Subscribe<GameSaveLoaded>(Handle);
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic, MessageBroker);
        }

        private void Handle(MessagePayload<GameSaveLoaded> obj)
        {
            Logic.State = new ValidateModuleState(Logic, MessageBroker);
            MessageBroker.Publish(this, new ValidateModule());
        }

        public override void EnterMainMenu()
        {
            MessageBroker.Publish(this, new EnterMainMenu());
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<MainMenuEntered>(Handle);
            MessageBroker.Unsubscribe<GameSaveLoaded>(Handle);
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
