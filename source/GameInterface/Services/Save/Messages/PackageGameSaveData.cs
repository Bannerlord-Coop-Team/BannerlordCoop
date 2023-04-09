using Common.Messaging;
using GameInterface.Services.Save.Data;
using System;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct PackageGameSaveData : ICommand
    {
        public Guid TransactionID { get; }

        public PackageGameSaveData(Guid transactionID)
        {
            TransactionID = transactionID;
        }
    }

    public readonly struct GameSaveDataPackaged : IResponse
    {
        public Guid TransactionID { get; }
        public byte[] GameSaveData { get; }
        public string CampaignID { get; }
        public GameObjectGuids GameObjectGuids { get; }

        /// <summary>
        /// GameSaveData will only be created internally as it requires game access
        /// </summary>
        public GameSaveDataPackaged(
            Guid transactionID, 
            byte[] gameSaveData, 
            string campaignID,
            GameObjectGuids gameObjectGuids)
        {
            TransactionID = transactionID;
            GameSaveData = gameSaveData;
            CampaignID = campaignID;
            GameObjectGuids = gameObjectGuids;
        }
}
}
