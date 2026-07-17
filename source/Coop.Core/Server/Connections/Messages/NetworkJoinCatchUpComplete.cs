using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Marks the end of the reliable-ordered updates withheld while a joining client loaded.
/// </summary>
[ProtoContract]
public record NetworkJoinCatchUpComplete : IEvent
{
}
