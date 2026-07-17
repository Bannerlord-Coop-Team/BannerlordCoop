using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Confirms that a joining client applied the ordered catch-up stream and its final snapshots.
/// </summary>
[ProtoContract]
public record NetworkJoinCatchUpApplied : IEvent
{
}
