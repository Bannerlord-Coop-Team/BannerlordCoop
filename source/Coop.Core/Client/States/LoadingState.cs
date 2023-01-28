using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.Services.GameState.Messages;
using System;

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
            MessageBroker.Subscribe<NetworkGameSaveDataRecieved>(Handle);
            MessageBroker.Subscribe<GameLoaded>(Handle);
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<MainMenuEntered>(Handle);
            MessageBroker.Unsubscribe<NetworkGameSaveDataRecieved>(Handle);
            MessageBroker.Unsubscribe<GameLoaded>(Handle);
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.Disconnect();
        }

        private void Handle(MessagePayload<NetworkGameSaveDataRecieved> obj)
        {
            MessageBroker.Publish(this, new LoadGameSave(obj.What.GameSaveData));
        }

        private void Handle(MessagePayload<GameLoaded> obj)
        {
            Logic.ResolveNetworkGuids();
        }

        public override void EnterMainMenu()
        {
            MessageBroker.Publish(this, new EnterMainMenu());
        }

        public override void ResolveNetworkGuids()
        {
            Logic.State = new ResolveNetworkGuidsState(Logic, MessageBroker);
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
