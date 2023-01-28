using Common.Messaging;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct LoadGameSave : ICommand
    {
        public byte[] SaveData { get; }

        public LoadGameSave(byte[] saveData)
        {
            SaveData = saveData;
        }
    }

    public readonly struct GameSaveLoaded : IEvent
    {
    }
}
