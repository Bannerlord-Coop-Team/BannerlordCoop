using Common.Messaging;
using GameInterface.Services.Save;
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

        /// <summary>
        /// GameSaveData will only be created internally as it requires game access
        /// </summary>
        /// <param name="gameSaveData">Game Save Data</param>
        public GameSaveDataPackaged(Guid transactionID, byte[] gameSaveData)
        {
            TransactionID = transactionID;
            GameSaveData = gameSaveData;
        }
    }
}
