using Common.Messaging;

namespace GameInterface.Services.Sessions.Messages;

/// <summary>
/// [Local] This client joined the session and must open its mission. The payload is the mod's own bytes from the
/// host; Coop never reads it.
/// </summary>
public record SessionMissionOpenRequested(string SessionId, string ModId, byte[] Payload) : IEvent;
