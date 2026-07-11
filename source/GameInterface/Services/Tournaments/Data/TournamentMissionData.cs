using ProtoBuf;
using System;

namespace GameInterface.Services.Tournaments.Data;

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentEquipmentElementData
{
    [ProtoMember(1)]
    public readonly int SlotIndex;
    [ProtoMember(2)]
    public readonly string ItemId;
    [ProtoMember(3)]
    public readonly string ItemModifierId;
    [ProtoMember(4)]
    public readonly string BannerCode;
    [ProtoMember(5)]
    public readonly short DataValue;
    [ProtoMember(6)]
    public readonly bool HasDataValue;

    public TournamentEquipmentElementData(int slotIndex, string itemId, string itemModifierId)
        : this(slotIndex, itemId, itemModifierId, null, 0, false)
    {
    }

    public TournamentEquipmentElementData(
        int slotIndex,
        string itemId,
        string itemModifierId,
        string bannerCode,
        short dataValue,
        bool hasDataValue = true)
    {
        SlotIndex = slotIndex;
        ItemId = itemId;
        ItemModifierId = itemModifierId;
        BannerCode = bannerCode;
        DataValue = dataValue;
        HasDataValue = hasDataValue;
    }
}

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
    public readonly TournamentEquipmentElementData[] Equipment;
    [ProtoMember(10)]
    public readonly float PositionX;
    [ProtoMember(11)]
    public readonly float PositionY;
    [ProtoMember(12)]
    public readonly float PositionZ;
    [ProtoMember(13)]
    public readonly float DirectionX;
    [ProtoMember(14)]
    public readonly float DirectionY;
    [ProtoMember(15)]
    public readonly float Health;
    [ProtoMember(16)]
    public readonly Guid MountAgentId;
    [ProtoMember(17)]
    public readonly string MountCharacterId;
    [ProtoMember(18)]
    public readonly int MountDescriptorSeed;
    [ProtoMember(19)]
    public readonly TournamentEquipmentElementData[] MountEquipment;
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
        TournamentEquipmentElementData[] equipment,
        float positionX,
        float positionY,
        float positionZ,
        float directionX,
        float directionY,
        float health,
        Guid mountAgentId,
        string mountCharacterId,
        int mountDescriptorSeed,
        TournamentEquipmentElementData[] mountEquipment,
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
            positionX,
            positionY,
            positionZ,
            directionX,
            directionY,
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
        TournamentEquipmentElementData[] equipment,
        float positionX,
        float positionY,
        float positionZ,
        float directionX,
        float directionY,
        float health,
        Guid mountAgentId,
        string mountCharacterId,
        int mountDescriptorSeed,
        TournamentEquipmentElementData[] mountEquipment,
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
        Equipment = equipment ?? Array.Empty<TournamentEquipmentElementData>();
        PositionX = positionX;
        PositionY = positionY;
        PositionZ = positionZ;
        DirectionX = directionX;
        DirectionY = directionY;
        Health = health;
        MountAgentId = mountAgentId;
        MountCharacterId = mountCharacterId;
        MountDescriptorSeed = mountDescriptorSeed;
        MountEquipment = mountEquipment ?? Array.Empty<TournamentEquipmentElementData>();
        MountHealth = mountHealth;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentSpawnManifestData
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly string MatchId;
    [ProtoMember(3)]
    public readonly long Revision;
    [ProtoMember(6)]
    public readonly long BracketRevision;
    [ProtoMember(4)]
    public readonly long Sequence;
    [ProtoMember(5)]
    public readonly TournamentAgentSpawnData[] Agents;

    public TournamentSpawnManifestData(
        string sessionId,
        string matchId,
        long revision,
        long bracketRevision,
        long sequence,
        TournamentAgentSpawnData[] agents)
    {
        SessionId = sessionId;
        MatchId = matchId;
        Revision = revision;
        BracketRevision = bracketRevision;
        Sequence = sequence;
        Agents = agents ?? Array.Empty<TournamentAgentSpawnData>();
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentTeamScoreData
{
    [ProtoMember(1)]
    public readonly string TeamId;
    [ProtoMember(2)]
    public readonly int Score;

    public TournamentTeamScoreData(string teamId, int score)
    {
        TeamId = teamId;
        Score = score;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class TournamentMatchResultData
{
    [ProtoMember(1)]
    public readonly string SessionId;
    [ProtoMember(2)]
    public readonly string MatchId;
    [ProtoMember(3)]
    public readonly long Revision;
    [ProtoMember(8)]
    public readonly long BracketRevision;
    [ProtoMember(4)]
    public readonly long Sequence;
    [ProtoMember(5)]
    public readonly string[] WinnerTeamIds;
    [ProtoMember(6)]
    public readonly string[] WinnerSlotIds;
    [ProtoMember(7)]
    public readonly TournamentTeamScoreData[] TeamScores;

    public TournamentMatchResultData(
        string sessionId,
        string matchId,
        long revision,
        long bracketRevision,
        long sequence,
        string[] winnerTeamIds,
        string[] winnerSlotIds,
        TournamentTeamScoreData[] teamScores)
    {
        SessionId = sessionId;
        MatchId = matchId;
        Revision = revision;
        BracketRevision = bracketRevision;
        Sequence = sequence;
        WinnerTeamIds = winnerTeamIds ?? Array.Empty<string>();
        WinnerSlotIds = winnerSlotIds ?? Array.Empty<string>();
        TeamScores = teamScores ?? Array.Empty<TournamentTeamScoreData>();
    }
}
