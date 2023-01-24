using Common.Messaging;
<<<<<<< HEAD
using GameInterface.Services.Save;
=======
using GameInterface.Data;
>>>>>>> NetworkEvent-refactor

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct PackageGameSaveData : ICommand
    {
    }

    public readonly struct GameSaveDataPackaged : IEvent
    {
        public IGameSaveData GameSaveData { get; }

        /// <summary>
        /// GameSaveData will only be created internally as it requires game access
        /// </summary>
        /// <param name="gameSaveData">Game Save Data</param>
        internal GameSaveDataPackaged(IGameSaveData gameSaveData)
        {
            GameSaveData = gameSaveData;
        }
    }
}
