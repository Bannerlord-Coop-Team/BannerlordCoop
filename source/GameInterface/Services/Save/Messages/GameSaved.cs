using Common.Messaging;

namespace GameInterface.Services.Save.Messages
{
    public readonly struct GameSaved : IEvent
    {
        public string SaveName { get; }

        public GameSaved(string saveName)
        {
            SaveName = saveName;
        }
    }
}
