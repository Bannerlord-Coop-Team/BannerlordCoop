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
            Logic.MessageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
            Logic.MessageBroker.Subscribe<CampaignStateEntered>(Handle_CampaignStateEntered);
        }

        public override void Dispose()
        {
            Logic.MessageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
            Logic.MessageBroker.Unsubscribe<CampaignStateEntered>(Handle_CampaignStateEntered);
        }

        public override void EnterCampaignState()
        {
            Logic.MessageBroker.Publish(this, new EnterCampaignState());
        }

        internal void Handle_CampaignStateEntered(MessagePayload<CampaignStateEntered> obj)
        {
            Logic.State = new CampaignState(Logic);
        }

        internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic);
        }

        public override void EnterMainMenu()
        {
            Logic.MessageBroker.Publish(this, new EnterMainMenu());
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
            Logic.MessageBroker.Publish(this, new EnterMainMenu());
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

        public override void ValidateModules()
        {
        }
    }
}
