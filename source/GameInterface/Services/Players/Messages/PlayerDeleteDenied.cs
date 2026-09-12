using Common.Messaging;

namespace GameInterface.Services.Players.Messages;

/// <summary>
/// Client-local event: the server denied this client's delete request
/// (<see cref="NetworkDeletePlayerDenied"/>). Carries the server's reason.
/// </summary>
public record PlayerDeleteDenied : IEvent
{
    public string Reason { get; }

    public PlayerDeleteDenied(string reason)
    {
        Reason = reason;
    }
}
