using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.Services.GameState.Messages;
using System;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State Logic Controller for the Receiving Saved Data State
    /// </summary>
    public class ReceivingSavedDataState : ClientStateBase
    {
        byte[] saveData;

        public ReceivingSavedDataState(IClientLogic logic) : base(logic)
        {
            Logic.NetworkMessageBroker.Subscribe<NetworkGameSaveDataRecieved>(Handle);
            Logic.NetworkMessageBroker.Subscribe<MainMenuEntered>(Handle);
        }

        public override void Dispose()
        {
            Logic.NetworkMessageBroker.Unsubscribe<NetworkGameSaveDataRecieved>(Handle);
            Logic.NetworkMessageBroker.Unsubscribe<MainMenuEntered>(Handle);
        }

        private void Handle(MessagePayload<NetworkGameSaveDataRecieved> obj)
        {
            saveData = obj.What.GameSaveData;
            Logic.EnterMainMenu();
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            if (saveData == null) return;

            var commandLoad = new LoadGameSave(saveData);
            Logic.NetworkMessageBroker.Publish(this, commandLoad);

            Logic.State = new LoadingState(Logic);
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

        public override void ResolveNetworkGuids()
        {
        }

        public override void ValidateModules()
        {
        }
    }
}
