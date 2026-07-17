using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Marks the reliable world-stream point at which a joining client can enter the map.
/// </summary>
[ProtoContract]
public record NetworkJoinWorldReady : IEvent
{
}
