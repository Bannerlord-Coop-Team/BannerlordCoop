using Common.Messaging;

namespace GameInterface.Services.Sessions.Messages;

/// <summary>[Local] Another member entered the session mission and this client replayed its state to it.</summary>
public record SessionMemberJoined(string SessionId, string ControllerId) : IEvent;
