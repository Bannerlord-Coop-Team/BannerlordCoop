using Common.Messaging;

namespace GameInterface.Services.Sessions.Messages;

/// <summary>
/// [Local] This client joined the session and must open its mission. The payload is the mod's own bytes from the
/// host; Coop never reads it. The mod opens its mission with the behaviors of <c>ISessionLauncher.CreateMissionBehaviors</c>
/// and spawns shared AI on the host only, because every member's spawn is replicated as an owned agent.
/// </summary>
public record SessionMissionOpenRequested(string SessionId, string ModId, byte[] Payload) : IEvent;
