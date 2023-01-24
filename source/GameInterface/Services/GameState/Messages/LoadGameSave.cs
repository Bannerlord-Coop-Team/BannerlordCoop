using Common.Messaging;
<<<<<<< HEAD
using GameInterface.Services.Save;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct GameSaveRecieved : IEvent
=======
using GameInterface.Data;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct LoadGameSave : ICommand
>>>>>>> NetworkEvent-refactor
    {
        public IGameSaveData SaveData { get; }
    }

    public readonly struct GameSaveLoaded : IEvent
    {
    }
}
