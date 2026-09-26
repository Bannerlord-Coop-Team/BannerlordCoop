using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Library;

namespace Missions.Agents.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAgentHitSound : IEvent
{
    [ProtoMember(1)]
    public Guid VictimAgentId { get; }

    [ProtoMember(2)]
    public bool IsMount { get; }

    [ProtoMember(3)]
    public int SoundIndex { get; }

    [ProtoMember(4)]
    public Vec3 Position { get; }

    [ProtoMember(5)]
    public float ArmorType { get; }

    public NetworkAgentHitSound(Guid victimAgentId, bool isMount,
        int soundIndex, Vec3 position, float armorType)
    {
        VictimAgentId = victimAgentId;
        IsMount = isMount;
        SoundIndex = soundIndex;
        Position = position;
        ArmorType = armorType;
    }
}
