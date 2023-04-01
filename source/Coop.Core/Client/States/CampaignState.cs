using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Time.Messages;
using System;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State controller for campaign client state
    /// </summary>
    public class CampaignState : ClientStateBase
    {
        public CampaignState(IClientLogic logic) : base(logic)
        {
            Logic.NetworkMessageBroker.Subscribe<NetworkDisableTimeControls>(Handle);

            Logic.NetworkMessageBroker.Subscribe<MainMenuEntered>(Handle);
            Logic.NetworkMessageBroker.Subscribe<MissionStateEntered>(Handle);
        }

        public override void Dispose()
        {
            Logic.NetworkMessageBroker.Unsubscribe<NetworkDisableTimeControls>(Handle);

            Logic.NetworkMessageBroker.Unsubscribe<MainMenuEntered>(Handle);
            Logic.NetworkMessageBroker.Unsubscribe<MissionStateEntered>(Handle);
        }

        private void Handle(MessagePayload<NetworkDisableTimeControls> obj)
        {
            // TODO will conflict with timemode changed event
            Logic.NetworkMessageBroker.Publish(this, new PauseAndDisableGameTimeControls());
        }

        public override void EnterMissionState()
        {
            Logic.NetworkMessageBroker.Publish(this, new EnterMissionState());
        }

        private void Handle(MessagePayload<MissionStateEntered> obj)
        {
            Logic.State = new MissionState(Logic);
        }

        public override void EnterMainMenu()
        {
            Logic.NetworkMessageBroker.Publish(this, new EnterMainMenu());
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic);
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
            Logic.NetworkMessageBroker.Publish(this, new EnterMainMenu());
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

        public override void ValidateModules()
        {
        }
    }
}
