using Common.Messaging;

namespace GameInterface.Services.UI.Messages;

public record HostSaveGame : ICommand
{
    public HostSaveGame(string saveName)
    {
        SaveName = saveName;
    }

    public string SaveName { get; }
}