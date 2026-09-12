using Common.Messaging;
using Common.Network.Session;

namespace GameInterface.Services.UI.Messages;

/// <summary>
/// Host the given save by spawning a managed server process and auto-joining it.
/// </summary>
public record AttemptHost : ICommand
{
    public AttemptHost(string saveName)
        : this(saveName, null, ServerVisibility.Public)
    {
    }

    public AttemptHost(string saveName, string password)
        : this(saveName, password, ServerVisibility.Public)
    {
    }

    public AttemptHost(string saveName, string password, ServerVisibility visibility)
    {
        SaveName = saveName;
        Password = password;
        Visibility = visibility;
    }

    public string SaveName { get; }
    public string Password { get; }
    public ServerVisibility Visibility { get; }
}
