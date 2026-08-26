using Common.Messaging;

namespace GameInterface.Services.UI.Messages;

/// <summary>
/// Published (via a relay from NetworkConnected) when the client has successfully
/// established a network session — direct or Steam lobby.  Used by the connection
/// UI to persist the last-session details only on confirmed success.
/// </summary>
public record ClientNetworkConnected : IEvent
{
    /// <summary>
    /// Password accepted by the server on this connection, if a native password
    /// dialog was shown. Null when no prompt was needed (open server or pre-supplied password).
    /// </summary>
    public string AcceptedPassword { get; init; }
}
