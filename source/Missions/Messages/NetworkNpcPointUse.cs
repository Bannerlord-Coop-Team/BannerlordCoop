using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Location NPC host to peers: an owned NPC started or stopped USING a scene point (chair,
/// animation point, usable machine). Scenes are identical on every client, so the receiver has its
/// puppet use the SAME local point (by native <c>MissionObjectId</c> — scene-placed ids are
/// deterministic) and all alignment, animation and occupancy run natively. This replaces
/// replicating the point's outputs (enforced actions, seat frames, facings), which required an
/// ever-growing pin apparatus on the receiver.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkNpcPointUse : IEvent
{
    [ProtoMember(1)]
    public readonly Guid AgentId;

    /// <summary>The point's scene MissionObjectId (never a runtime-created id). Meaningless when
    /// <see cref="InUse"/> is false.</summary>
    [ProtoMember(2)]
    public readonly int PointId;

    [ProtoMember(3)]
    public readonly bool InUse;

    public NetworkNpcPointUse(Guid agentId, int pointId, bool inUse)
    {
        AgentId = agentId;
        PointId = pointId;
        InUse = inUse;
    }
}
