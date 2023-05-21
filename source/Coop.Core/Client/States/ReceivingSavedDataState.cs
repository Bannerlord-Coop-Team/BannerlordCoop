﻿using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.Heroes.Handlers;
using GameInterface.Services.Heroes.Messages;
using System;
using GameInterface.Services.GameState.Messages;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State Logic Controller for the Receiving Saved Data State
    /// </summary>
    public class ReceivingSavedDataState : ClientStateBase
    {
        private NetworkGameSaveDataReceived saveDataMessage = default;

        public ReceivingSavedDataState(IClientLogic logic) : base(logic)
        {
            Logic.MessageBroker.Subscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
            Logic.MessageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        }

        public override void Dispose()
        {
            Logic.MessageBroker.Unsubscribe<NetworkGameSaveDataReceived>(Handle_NetworkGameSaveDataReceived);
            Logic.MessageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        }

        internal void Handle_NetworkGameSaveDataReceived(MessagePayload<NetworkGameSaveDataReceived> obj)
        {
            saveDataMessage = obj.What;
            Logic.EnterMainMenu();
        }

        internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
        {
            var saveData = saveDataMessage.GameSaveData;

            if (saveData == null) return;
            if (saveData.Length == 0) return;

            var commandLoad = new LoadGameSave(saveData);
            Logic.MessageBroker.Publish(this, commandLoad);

            Logic.LoadSavedData();
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
            Logic.State = new MainMenuState(Logic);
        }

        public override void ExitGame()
        {
        }

        public override void LoadSavedData()
        {
            Logic.State = new LoadingState(Logic);
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

        public override void ValidateModules()
        {
        }
    }
}
