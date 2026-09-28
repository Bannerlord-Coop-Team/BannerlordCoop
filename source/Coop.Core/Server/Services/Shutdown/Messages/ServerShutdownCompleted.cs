using Common.Messaging;

namespace Coop.Core.Server.Services.Shutdown.Messages;

/// <summary>Published once every player was disconnected and both the save and its session JSON were written.</summary>
public readonly struct ServerShutdownCompleted : IEvent
{
    public string SaveName { get; }

    public ServerShutdownCompleted(string saveName)
    {
        SaveName = saveName;
    }
}
