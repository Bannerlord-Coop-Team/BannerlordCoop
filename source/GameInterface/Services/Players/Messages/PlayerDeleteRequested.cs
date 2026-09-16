using Common.Messaging;

namespace GameInterface.Services.Players.Messages;

/// <summary>
/// Requests deletion of this player, optionally staying connected to view game over statistics.
/// </summary>
public record PlayerDeleteRequested : IEvent
{
    public bool KeepConnected { get; }

    public PlayerDeleteRequested(bool keepConnected = false)
    {
        KeepConnected = keepConnected;
    }
}
