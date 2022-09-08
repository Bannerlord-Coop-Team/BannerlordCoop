using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State Logic Controller for the Mission (Battles) Client State
    /// </summary>
    public class MissionState : ClientStateBase
    {
        public MissionState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<MainMenuEntered>(Handle);
            MessageBroker.Subscribe<CampaignStateEntered>(Handle);
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic, MessageBroker);
        }

        private void Handle(MessagePayload<CampaignStateEntered> obj)
        {
            Logic.State = new CampaignState(Logic, MessageBroker);
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<MainMenuEntered>(Handle);
            MessageBroker.Unsubscribe<CampaignStateEntered>(Handle);
        }

        public override void EnterCampaignState()
        {
            MessageBroker.Publish(this, new EnterCampaignState());
        }

        public override void EnterMainMenu()
        {
            MessageBroker.Publish(this, new EnterMainMenu());
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
        }

        public override void EnterMissionState()
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

        public override void ResolveNetworkGuids()
        {
            throw new System.NotImplementedException();
        }
    }
}
