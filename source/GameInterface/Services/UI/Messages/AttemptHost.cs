using Common.Messaging;

namespace GameInterface.Services.UI.Messages;

/// <summary>
/// Host the given save by spawning a managed server process and auto-joining it.
/// </summary>
public record AttemptHost : ICommand
{
    public AttemptHost(string saveName)
        : this(saveName, null)
    {
    }

    public AttemptHost(string saveName, string password)
    {
        SaveName = saveName;
        Password = password;
    }

    public string SaveName { get; }
    public string Password { get; }
}
