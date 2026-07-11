using Common.Messaging;
using GameInterface.Services.Tournaments.Data;
using ProtoBuf;
using System;
using TaleWorlds.Library;

namespace Missions.Tournaments.Messages;

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentMissionWeaponData
{
    [ProtoMember(1)] public readonly int SlotIndex;
    [ProtoMember(2)] public readonly string ItemId;
    [ProtoMember(3)] public readonly string ItemModifierId;
    [ProtoMember(4)] public readonly string BannerCode;
    [ProtoMember(5)] public readonly short DataValue;

    public TournamentMissionWeaponData(
        int slotIndex,
        string itemId,
        string itemModifierId,
        string bannerCode,
        short dataValue)
    {
        SlotIndex = slotIndex;
        ItemId = itemId;
        ItemModifierId = itemModifierId;
        BannerCode = bannerCode;
        DataValue = dataValue;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentAgentRuntimeData
{
    [ProtoMember(1)] public readonly Guid AgentId;
    [ProtoMember(2)] public readonly float Health;
    [ProtoMember(3)] public readonly TournamentMissionWeaponData[] Equipment;

    public TournamentAgentRuntimeData(Guid agentId, float health)
        : this(agentId, health, Array.Empty<TournamentMissionWeaponData>())
    {
    }

    public TournamentAgentRuntimeData(
        Guid agentId,
        float health,
        TournamentMissionWeaponData[] equipment)
    {
        AgentId = agentId;
        Health = health;
        Equipment = equipment ?? Array.Empty<TournamentMissionWeaponData>();
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentWorldItemRuntimeData
{
    [ProtoMember(1)] public readonly Guid WorldItemId;
    [ProtoMember(2)] public readonly string ItemId;
    [ProtoMember(3)] public readonly string ItemModifierId;
    [ProtoMember(4)] public readonly string BannerCode;
    [ProtoMember(5)] public readonly short DataValue;
    [ProtoMember(6)] public readonly Vec3 Position;
    [ProtoMember(7)] public readonly Mat3 Rotation;
    [ProtoMember(8)] public readonly int SpawnFlags;
    [ProtoMember(9)] public readonly bool HasLifeTime;

    public TournamentWorldItemRuntimeData(
        Guid worldItemId,
        string itemId,
        string itemModifierId,
        string bannerCode,
        short dataValue,
        Vec3 position,
        Mat3 rotation,
        int spawnFlags,
        bool hasLifeTime)
    {
        WorldItemId = worldItemId;
        ItemId = itemId;
        ItemModifierId = itemModifierId;
        BannerCode = bannerCode;
        DataValue = dataValue;
        Position = position;
        Rotation = rotation;
        SpawnFlags = spawnFlags;
        HasLifeTime = hasLifeTime;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkTournamentRuntimeState : IEvent
{
    [ProtoMember(1)] public readonly string SessionId;
    [ProtoMember(2)] public readonly string MatchId;
    [ProtoMember(3)] public readonly long Revision;
    [ProtoMember(4)] public readonly string OriginControllerId;
    [ProtoMember(5)] public readonly long Sequence;
    [ProtoMember(6)] public readonly Guid[] AliveAgentIds;
    [ProtoMember(7)] public readonly string[] AliveSlotIds;
    [ProtoMember(8)] public readonly string[] AliveTeamIds;
    [ProtoMember(9)] public readonly TournamentTeamScoreData[] TeamScores;
    [ProtoMember(10)] public readonly TournamentAgentRuntimeData[] Agents;
    [ProtoMember(11)] public readonly TournamentWorldItemRuntimeData[] WorldItems;

    public NetworkTournamentRuntimeState(
        string sessionId,
        string matchId,
        long revision,
        string originControllerId,
        long sequence,
        Guid[] aliveAgentIds,
        string[] aliveSlotIds,
        string[] aliveTeamIds,
        TournamentTeamScoreData[] teamScores,
        TournamentAgentRuntimeData[] agents)
        : this(
            sessionId,
            matchId,
            revision,
            originControllerId,
            sequence,
            aliveAgentIds,
            aliveSlotIds,
            aliveTeamIds,
            teamScores,
            agents,
            Array.Empty<TournamentWorldItemRuntimeData>())
    {
    }

    public NetworkTournamentRuntimeState(
        string sessionId,
        string matchId,
        long revision,
        string originControllerId,
        long sequence,
        Guid[] aliveAgentIds,
        string[] aliveSlotIds,
        string[] aliveTeamIds,
        TournamentTeamScoreData[] teamScores,
        TournamentAgentRuntimeData[] agents,
        TournamentWorldItemRuntimeData[] worldItems)
    {
        SessionId = sessionId;
        MatchId = matchId;
        Revision = revision;
        OriginControllerId = originControllerId;
        Sequence = sequence;
        AliveAgentIds = aliveAgentIds ?? Array.Empty<Guid>();
        AliveSlotIds = aliveSlotIds ?? Array.Empty<string>();
        AliveTeamIds = aliveTeamIds ?? Array.Empty<string>();
        TeamScores = teamScores ?? Array.Empty<TournamentTeamScoreData>();
        Agents = agents ?? Array.Empty<TournamentAgentRuntimeData>();
        WorldItems = worldItems ?? Array.Empty<TournamentWorldItemRuntimeData>();
    }
}
