using Common.Messaging;
using GameInterface.Services.Save;
using System;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct PackageGameSaveData : ICommand
    {
        public Guid TransfeId { get; }

        public PackageGameSaveData(Guid transferId)
        {
            TransfeId = transferId;
        }
    }

    public readonly struct GameSaveDataPackaged : IEvent
    {
        public Guid TransfeId { get; }
        public byte[] GameSaveData { get; }

        /// <summary>
        /// GameSaveData will only be created internally as it requires game access
        /// </summary>
        /// <param name="gameSaveData">Game Save Data</param>
        internal GameSaveDataPackaged(Guid transfeId, byte[] gameSaveData)
        {
            TransfeId = transfeId;
            GameSaveData = gameSaveData;
        }
    }
}
