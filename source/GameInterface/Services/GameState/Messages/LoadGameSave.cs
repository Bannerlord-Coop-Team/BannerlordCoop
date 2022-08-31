using Common.Messaging;
using GameInterface.Data;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct LoadGameSave : ICommand
    {
        public IGameSaveData SaveData { get; }
    }

    public readonly struct GameSaveLoaded : IEvent
    {
    }
}
