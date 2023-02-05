using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.Messages.Outgoing;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Time.Messages;
using System;

namespace Coop.Core.Server.Connections.States
{
    public class TransferSaveState : ConnectionStateBase
    {
        private Guid TransferId;

        public TransferSaveState(IConnectionLogic connectionLogic)
            : base(connectionLogic)
        {
            // maybe remove?
            ConnectionLogic.NetworkMessageBroker.Publish(this, new PlayerRecievingSave(ConnectionLogic.PlayerId));

            ConnectionLogic.NetworkMessageBroker.Subscribe<GameSaveDataPackaged>(Handle);

            ConnectionLogic.NetworkMessageBroker.PublishNetworkEvent(new NetworkDisableTimeControls());
            // TODO will conflict with timemode changed event
            ConnectionLogic.NetworkMessageBroker.Publish(this, new PauseAndDisableGameTimeControls());

            TransferId = Guid.NewGuid();
            ConnectionLogic.NetworkMessageBroker.Publish(this, new PackageGameSaveData(TransferId));
        }

        public override void Dispose()
        {
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<GameSaveDataPackaged>(Handle);
        }

        private void Handle(MessagePayload<GameSaveDataPackaged> obj)
        {
            var transferId = obj.What.TransfeId;

            if(TransferId == transferId)
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
