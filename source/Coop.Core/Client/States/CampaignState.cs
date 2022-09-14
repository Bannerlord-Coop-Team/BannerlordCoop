using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State controller for campaign client state
    /// </summary>
    public class CampaignState : ClientStateBase
    {
        public CampaignState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<MainMenuEntered>(Handle);
            MessageBroker.Subscribe<MissionStateEntered>(Handle);
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic, MessageBroker);
        }

        private void Handle(MessagePayload<MissionStateEntered> obj)
        {
            Logic.State = new MissionState(Logic, MessageBroker);
        }

        public override void EnterMissionState()
        {
            MessageBroker.Publish(this, new EnterMissionState());
        }

        public override void EnterMainMenu()
        {
            MessageBroker.Publish(this, new EnterMainMenu());
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<MainMenuEntered>(Handle);
            MessageBroker.Unsubscribe<MissionStateEntered>(Handle);
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
            MessageBroker.Publish(this, new ExitGame());
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

        public override void ResolveNetworkGuids()
        {
        }
    }
}
