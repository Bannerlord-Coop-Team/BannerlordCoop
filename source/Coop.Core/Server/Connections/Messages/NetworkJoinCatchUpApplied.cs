using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Confirms that a joining client applied its refreshed baseline and caught up to server time.
/// </summary>
[ProtoContract]
public record NetworkJoinCatchUpApplied : IEvent
{
}
