using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State Logic Controller for the Loading Client State
    /// </summary>
    public class LoadingState : ClientStateBase
    {
        public LoadingState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<MainMenuEntered>(Handle);
            MessageBroker.Subscribe<NetworkGuidsResolved>(Handle);
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic, MessageBroker);
        }

        private void Handle(MessagePayload<NetworkGuidsResolved> obj)
        {
            Logic.State = new ResolveNetworkGuidsState(Logic, MessageBroker);
        }

        public override void EnterMainMenu()
        {
            MessageBroker.Publish(this, new EnterMainMenu());
        }

        public override void ResolveNetworkGuids()
        {
            MessageBroker.Publish(this, new ResolveNetworkGuids());
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<MainMenuEntered>(Handle);
            MessageBroker.Unsubscribe<NetworkGuidsResolved>(Handle);
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
    }
}
