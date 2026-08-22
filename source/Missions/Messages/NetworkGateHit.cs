using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Ram simulator to everyone: its battering ram struck a gate. The host applies the damage with vanilla
/// TriggerOnHit even when another peer simulates the gate; non-hosts replay the door/plank flinch, heavy-hit
/// particles and impact sound.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkGateHit : IEvent
{
    [ProtoMember(1)]
    public int GateId { get; }
    [ProtoMember(2)]
    public int RamId { get; }
    [ProtoMember(3)]
    public int Damage { get; }

    public NetworkGateHit(int gateId, int ramId, int damage)
    {
        GateId = gateId;
        RamId = ramId;
        Damage = damage;
    }
}
