using Common.Messaging;
using GameInterface.Services.Save;
using System;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct PackageGameSaveData : ICommand
    {
        public int PeerId { get; }

        public PackageGameSaveData(int peerId)
        {
            PeerId = peerId;
        }
    }

    public readonly struct GameSaveDataPackaged : IEvent
    {
        public int PeerId { get; }
        public byte[] GameSaveData { get; }

        /// <summary>
        /// GameSaveData will only be created internally as it requires game access
        /// </summary>
        /// <param name="gameSaveData">Game Save Data</param>
        public GameSaveDataPackaged(int peerId, byte[] gameSaveData)
        {
            PeerId = peerId;
            GameSaveData = gameSaveData;
        }
    }
}
