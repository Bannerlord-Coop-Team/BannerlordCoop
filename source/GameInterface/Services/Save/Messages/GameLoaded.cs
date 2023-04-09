using Common.Messaging;

namespace GameInterface.Services.Save.Messages
{
    public readonly struct GameLoaded : IEvent
    {
        public string SaveName { get; }

        public GameLoaded(string saveName)
        {
            SaveName = saveName;
        }
    }
}
