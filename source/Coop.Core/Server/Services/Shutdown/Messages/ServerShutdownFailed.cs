using Common.Messaging;

namespace Coop.Core.Server.Services.Shutdown.Messages;

/// <summary>Published when a started shutdown could not confirm its save.</summary>
public readonly struct ServerShutdownFailed : IEvent
{
    public string Reason { get; }

    public ServerShutdownFailed(string reason)
    {
        Reason = reason;
    }
}
