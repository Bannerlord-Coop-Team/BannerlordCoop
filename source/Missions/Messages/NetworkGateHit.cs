using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Mission host to peers: a battering ram struck a gate hard enough for the gate hit reaction. The peer replays
/// the door/plank flinch animation, the heavy-hit particles and the impact sound on that gate — its own gate
/// never runs OnHit, so nothing reacts otherwise. Gate damage/destruction is synced separately.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkGateHit : IEvent
{
    [ProtoMember(1)]
    public int GateId { get; }

    public NetworkGateHit(int gateId)
    {
        GateId = gateId;
    }
}
