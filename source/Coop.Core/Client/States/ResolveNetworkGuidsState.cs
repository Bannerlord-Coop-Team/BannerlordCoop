using Common.Messaging;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State Logic Controller for the Resolve Network Guids State
    /// </summary>
    public class ResolveNetworkGuidsState : ClientStateBase
    {
        public ResolveNetworkGuidsState(IClientLogic logic) : base(logic)
        {
            Logic.NetworkMessageBroker.Publish(this, new ResolveNetworkGuids());

            Logic.NetworkMessageBroker.Subscribe<MainMenuEntered>(Handle);
            Logic.NetworkMessageBroker.Subscribe<CampaignStateEntered>(Handle);

            // TODO implement and remove state skip
            Dispose();
            Logic.State = new CampaignState(logic);
        }

        public override void Dispose()
        {
            Logic.NetworkMessageBroker.Unsubscribe<MainMenuEntered>(Handle);
            Logic.NetworkMessageBroker.Unsubscribe<CampaignStateEntered>(Handle);
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic);
        }

        private void Handle(MessagePayload<CampaignStateEntered> obj)
        {
            Logic.State = new CampaignState(Logic);
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
