using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State Logic Controller for the Mission (Battles) Client State
    /// </summary>
    public class MissionState : ClientStateBase
    {
        public MissionState(IClientLogic logic) : base(logic)
        {
            Logic.NetworkMessageBroker.Subscribe<MainMenuEntered>(Handle);
            Logic.NetworkMessageBroker.Subscribe<CampaignStateEntered>(Handle);
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic);
        }

        private void Handle(MessagePayload<CampaignStateEntered> obj)
        {
            Logic.State = new CampaignState(Logic);
        }

        public override void Dispose()
        {
            Logic.NetworkMessageBroker.Unsubscribe<MainMenuEntered>(Handle);
            Logic.NetworkMessageBroker.Unsubscribe<CampaignStateEntered>(Handle);
        }

        public override void EnterCampaignState()
        {
            Logic.NetworkMessageBroker.Publish(this, new EnterCampaignState());
        }

        public override void EnterMainMenu()
        {
            Logic.NetworkMessageBroker.Publish(this, new EnterMainMenu());
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
            Logic.NetworkMessageBroker.Publish(this, new EnterMainMenu());
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
        }
    }
}
