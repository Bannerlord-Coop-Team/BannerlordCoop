using Common.Messaging;

namespace GameInterface.Services.Sessions.Messages;

/// <summary>[Local] The session ended (closed by the host, or the server was lost). The mod ends its own mission in response.</summary>
public record SessionMissionEndRequested(string SessionId) : IEvent;
