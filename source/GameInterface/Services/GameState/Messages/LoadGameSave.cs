using Common.Messaging;
using GameInterface.Services.Save;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct GameSaveRecieved : IEvent
    {
        public IGameSaveData SaveData { get; }
    }

    public readonly struct GameSaveLoaded : IEvent
    {
    }
}
