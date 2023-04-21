﻿using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Time.Messages;
using System;

namespace Coop.Core.Server.Connections.States
{
    public class TransferSaveState : ConnectionStateBase
    {
        public TransferSaveState(IConnectionLogic connectionLogic)
            : base(connectionLogic)
        {
            ConnectionLogic.NetworkMessageBroker.Subscribe<GameSaveDataPackaged>(Handle);

            ConnectionLogic.NetworkMessageBroker.PublishNetworkEvent(new NetworkDisableTimeControls());
            // TODO will conflict with timemode changed event
            ConnectionLogic.NetworkMessageBroker.Publish(this, new PauseAndDisableGameTimeControls());

            ConnectionLogic.NetworkMessageBroker.Publish(this, new PackageGameSaveData(ConnectionLogic.PlayerId.Id));
        }

        public override void Dispose()
        {
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<GameSaveDataPackaged>(Handle);
        }

        private void Handle(MessagePayload<GameSaveDataPackaged> obj)
        {
            var peerId = obj.What.PeerId;

            if(peerId == ConnectionLogic.PlayerId.Id)
            {
                var saveData = obj.What.GameSaveData;
                var peer = ConnectionLogic.PlayerId;
                ConnectionLogic.NetworkMessageBroker.PublishNetworkEvent(peer, new NetworkGameSaveDataRecieved(saveData));

                ConnectionLogic.Load();
            }
        }

        public override void CreateCharacter()
        {
        }

        public override void EnterCampaign()
        {
        }

        public override void EnterMission()
        {
        }

        public override void Load()
        {
            ConnectionLogic.State = new LoadingState(ConnectionLogic);
        }

        public override void TransferSave()
        {
        }
    }
}