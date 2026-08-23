using Common.Messaging;

namespace GameInterface.Services.UI.Messages;

/// <summary>
/// Published (via a relay from NetworkConnected) when the client has successfully
/// established a network session — direct or Steam lobby.  Used by the connection
/// UI to persist the last-session details only on confirmed success.
/// </summary>
public record ClientNetworkConnected : IEvent { }
