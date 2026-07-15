using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Tournaments.Data;

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentAgentSpawnData
{
    [ProtoMember(1)]
    public readonly Guid AgentId;
    [ProtoMember(2)]
    public readonly string SlotId;
    [ProtoMember(3)]
    public readonly string CharacterId;
    [ProtoMember(4)]
    public readonly int DescriptorSeed;
    [ProtoMember(5)]
    public readonly string TeamId;
    [ProtoMember(6)]
    public readonly uint TeamColor;
    [ProtoMember(7)]
    public readonly string TeamBannerCode;
    [ProtoMember(8)]
    public readonly string ControllerId;
    [ProtoMember(9)]
    public readonly EquipmentElement[] Equipment;
    [ProtoMember(10)]
    public readonly Vec3 Position;
    [ProtoMember(11)]
    public readonly Vec2 Direction;
    [ProtoMember(15)]
    public readonly float Health;
    [ProtoMember(16)]
    public readonly Guid MountAgentId;
    [ProtoMember(17)]
    public readonly string MountCharacterId;
    [ProtoMember(18)]
    public readonly int MountDescriptorSeed;
    [ProtoMember(19)]
    public readonly EquipmentElement[] MountEquipment;
    [ProtoMember(20)]
    public readonly float MountHealth;
    [ProtoMember(21)]
    public readonly uint TeamColor2;

    public TournamentAgentSpawnData(
        Guid agentId,
        string slotId,
        string characterId,
        int descriptorSeed,
        string teamId,
        uint teamColor,
        string teamBannerCode,
        string controllerId,
        EquipmentElement[] equipment,
        Vec3 position,
        Vec2 direction,
        float health,
        Guid mountAgentId,
        string mountCharacterId,
        int mountDescriptorSeed,
        EquipmentElement[] mountEquipment,
        float mountHealth)
        : this(
            agentId,
            slotId,
            characterId,
            descriptorSeed,
            teamId,
            teamColor,
            uint.MaxValue,
            teamBannerCode,
            controllerId,
            equipment,
            position,
            direction,
            health,
            mountAgentId,
            mountCharacterId,
            mountDescriptorSeed,
            mountEquipment,
            mountHealth)
    {
    }

    public TournamentAgentSpawnData(
        Guid agentId,
        string slotId,
        string characterId,
        int descriptorSeed,
        string teamId,
        uint teamColor,
        uint teamColor2,
        string teamBannerCode,
        string controllerId,
        EquipmentElement[] equipment,
        Vec3 position,
        Vec2 direction,
        float health,
        Guid mountAgentId,
        string mountCharacterId,
        int mountDescriptorSeed,
        EquipmentElement[] mountEquipment,
        float mountHealth)
    {
        AgentId = agentId;
        SlotId = slotId;
        CharacterId = characterId;
        DescriptorSeed = descriptorSeed;
        TeamId = teamId;
        TeamColor = teamColor;
        TeamColor2 = teamColor2;
        TeamBannerCode = teamBannerCode;
        ControllerId = controllerId;
        Equipment = equipment ?? Array.Empty<EquipmentElement>();
        Position = position;
        Direction = direction;
        Health = health;
        MountAgentId = mountAgentId;
        MountCharacterId = mountCharacterId;
        MountDescriptorSeed = mountDescriptorSeed;
        MountEquipment = mountEquipment ?? Array.Empty<EquipmentElement>();
        MountHealth = mountHealth;
    }
}
