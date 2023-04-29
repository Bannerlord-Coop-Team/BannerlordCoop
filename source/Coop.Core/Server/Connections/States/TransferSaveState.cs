using Common.Messaging;
using Common.Serialization;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Save.Data;
using GameInterface.Services.Time.Messages;
using System;

namespace Coop.Core.Server.Connections.States
{
    public class TransferSaveState : ConnectionStateBase
    {
        private Guid PackageGameTransactionId;

        public TransferSaveState(IConnectionLogic connectionLogic)
            : base(connectionLogic)
        {
            ConnectionLogic.NetworkMessageBroker.Subscribe<GameSaveDataPackaged>(Handle);

            ConnectionLogic.NetworkMessageBroker.PublishNetworkEvent(new NetworkDisableTimeControls());
            // TODO will conflict with timemode changed event
            ConnectionLogic.NetworkMessageBroker.Publish(this, new PauseAndDisableGameTimeControls());

            PackageGameTransactionId = Guid.NewGuid();
            ConnectionLogic.NetworkMessageBroker.Publish(this, new PackageGameSaveData(PackageGameTransactionId));
        }

        public override void Dispose()
        {
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<GameSaveDataPackaged>(Handle);
        }

        
        private void Handle(MessagePayload<GameSaveDataPackaged> obj)
        {
            var payload = obj.What;
            if(PackageGameTransactionId == payload.TransactionID)
            {
                var peer = ConnectionLogic.PlayerId;
                var networkEvent = new NetworkGameSaveDataReceived(
                    payload.GameSaveData,
                    payload.CampaignID,
                    payload.GameObjectGuids);

                byte[] test = ProtoBufSerializer.Serialize(networkEvent);
                ProtoBufSerializer.Deserialize(test);

                ConnectionLogic.NetworkMessageBroker.PublishNetworkEvent(peer, networkEvent);

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
