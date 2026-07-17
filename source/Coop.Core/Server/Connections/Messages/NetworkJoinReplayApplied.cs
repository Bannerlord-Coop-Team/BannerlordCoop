using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Connections.Messages;

/// <summary>
/// Confirms that a joining client applied every game-thread action before the replay marker.
/// </summary>
[ProtoContract]
public record NetworkJoinReplayApplied : ICommand
{
}
