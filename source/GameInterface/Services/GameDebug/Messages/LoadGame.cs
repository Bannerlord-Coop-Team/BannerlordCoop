using Common.Messaging;

namespace GameInterface.Services.GameDebug.Messages;

public record LoadGame : ICommand
{
    public LoadGame(string saveName)
    {
        SaveName = saveName;
    }

    public string SaveName { get; }
}
