using Common.Messaging;

namespace GameInterface.Services.Sessions.Messages;

/// <summary>
/// [Local] This client heard of another member entering the session mission and replayed its state to it. It is
/// raised once per peer link: on each existing member once for the newcomer, and on the newcomer once per existing member.
/// </summary>
public record SessionMemberJoined(string SessionId, string ControllerId) : IEvent;
