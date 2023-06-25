using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// A player has entered a battle event
/// </summary>
[ProtoContract]
public record NetworkPlayerMissionEntered : IEvent
{
}
