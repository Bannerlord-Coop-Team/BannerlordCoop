using Common.Messaging;

namespace GameInterface.Services.Sessions.Messages;

/// <summary>
/// [Local] The host of the session mission changed, or was assigned for the first time. A mod runs its
/// authoritative logic only while <see cref="IsLocalHost"/> is true.
/// </summary>
public record SessionHostChanged(string SessionId, string HostControllerId, int Epoch, bool IsLocalHost) : IEvent;
