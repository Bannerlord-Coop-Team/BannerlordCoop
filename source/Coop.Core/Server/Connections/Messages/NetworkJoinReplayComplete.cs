using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Marks the end of the reliable world replay sent to a joining client.
/// </summary>
[ProtoContract]
public record NetworkJoinReplayComplete : IEvent
{
}
