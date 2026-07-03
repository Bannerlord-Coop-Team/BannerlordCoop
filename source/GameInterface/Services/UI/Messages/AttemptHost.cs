using Common.Messaging;

namespace GameInterface.Services.UI.Messages;

/// <summary>
/// Host the given save by spawning a managed server process and auto-joining it.
/// </summary>
public record AttemptHost : ICommand
{
    public AttemptHost(string saveName)
    {
        SaveName = saveName;
    }

    public string SaveName { get; }
}
