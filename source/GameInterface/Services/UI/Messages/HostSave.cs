using Common.Messaging;

namespace GameInterface.Services.UI.Messages;

public record HostSave : ICommand
{
    public HostSave(string saveName)
    {
        SaveName = saveName;
    }

    public string SaveName { get; }
}