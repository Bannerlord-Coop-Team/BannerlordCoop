using Common.Messaging;

namespace Coop.Core.Server.Services.Shutdown.Messages;

/// <summary>
/// Asks the server to restart after <see cref="Seconds"/>. Broker-only: it has no ProtoContract,
/// and the coordinator ignores it when it arrives from a network peer.
/// </summary>
public readonly struct RequestServerShutdown : ICommand
{
    public int Seconds { get; }

    public RequestServerShutdown(int seconds)
    {
        Seconds = seconds;
    }
}
