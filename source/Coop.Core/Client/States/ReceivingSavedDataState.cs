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
        public ReceivingSavedDataState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<NetworkGameSaveDataRecieved>(Handle);
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<NetworkGameSaveDataRecieved>(Handle);
        }

        private void Handle(MessagePayload<NetworkGameSaveDataRecieved> obj)
        {
            var commandLoad = new LoadGameSave(obj.What.GameSaveData);
            MessageBroker.Publish(this, commandLoad);

            Logic.State = new ValidateModuleState(Logic, MessageBroker);
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

        public override void ResolveNetworkGuids()
        {
        }
    }
}
