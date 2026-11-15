using Common.Messaging;

namespace GameInterface.Services.Sessions.Messages;

/// <summary>[Local] The server closed the session. The mod ends its own mission in response.</summary>
public record SessionMissionEndRequested(string SessionId) : IEvent;
