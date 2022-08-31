using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States
{
    public class ValidateModuleState : ClientStateBase
    {
        public ValidateModuleState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<MainMenuEntered>(Handle);
            MessageBroker.Subscribe<ModulesValidated>(Handle);
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic, MessageBroker);
        }

        private void Handle(MessagePayload<ModulesValidated> obj)
        {
            Logic.State = new LoadingState(Logic, MessageBroker);
        }

        public override void EnterMainMenu()
        {
            MessageBroker.Publish(this, new EnterMainMenu());
        }

        public override void LoadSavedData()
        {
            MessageBroker.Publish(this, new ValidateModule());
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<MainMenuEntered>(Handle);
            MessageBroker.Unsubscribe<ModulesValidated>(Handle);
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
        }

        public override void EnterCampaignState()
        {
        }

        public override void EnterMissionState()
        {
        }

        public override void ExitGame()
        {
        }

        public override void StartCharacterCreation()
        {
        }

        public override void ResolveNetworkGuids()
        {
        }
    }
}
