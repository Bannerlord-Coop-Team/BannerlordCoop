using Common.Messaging;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages.Outgoing;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Time.Messages;
using System;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State controller for campaign client state
    /// </summary>
    public class CampaignState : ClientStateBase
    {
        public CampaignState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<NetworkDisableTimeControls>(Handle);

            MessageBroker.Subscribe<MainMenuEntered>(Handle);
            MessageBroker.Subscribe<MissionStateEntered>(Handle);
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<NetworkDisableTimeControls>(Handle);

            MessageBroker.Unsubscribe<MainMenuEntered>(Handle);
            MessageBroker.Unsubscribe<MissionStateEntered>(Handle);
        }

        private void Handle(MessagePayload<NetworkDisableTimeControls> obj)
        {
            // TODO will conflict with timemode changed event
            MessageBroker.Publish(this, new PauseAndDisableGameTimeControls());
        }

        public override void EnterMissionState()
        {
            MessageBroker.Publish(this, new EnterMissionState());
        }

        private void Handle(MessagePayload<MissionStateEntered> obj)
        {
            Logic.State = new MissionState(Logic, MessageBroker);
        }

        public override void EnterMainMenu()
        {
            MessageBroker.Publish(this, new EnterMainMenu());
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic, MessageBroker);
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

        public override void ResolveNetworkGuids()
        {
        }
    }
}
