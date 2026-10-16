using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;

namespace Missions.Messages;

/// <summary>
/// Owner to peers over the mission mesh: the hulls this owner fielded, sent at its first deployment commit so
/// peers can spawn kinematic copies before its crew records need them.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkSpawnBattleShips : IEvent
{
    [ProtoMember(1)] public readonly BattleShipSpawnData[] Ships;

    public NetworkSpawnBattleShips(BattleShipSpawnData[] ships)
    {
        Ships = ships ?? Array.Empty<BattleShipSpawnData>();
    }
}

/// <summary>
/// One hull: its network identity and owner, where it is, and the campaign ship description peers rebuild it
/// from (peers have no synced campaign Ship).
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class BattleShipSpawnData
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string OwnerControllerId;
    [ProtoMember(3)] public readonly string MapEventPartyId;
    [ProtoMember(4)] public readonly bool IsNpcParty;
    [ProtoMember(5)] public readonly BattleSideEnum Side;
    [ProtoMember(6)] public readonly int FormationIndex;
    [ProtoMember(7)] public readonly float[] Frame;
    [ProtoMember(8)] public readonly string HullId;
    [ProtoMember(9)] public readonly string[] PieceSlots;
    [ProtoMember(10)] public readonly string[] PieceIds;
    [ProtoMember(11)] public readonly string FigureheadId;
    [ProtoMember(12)] public readonly string Name;
    [ProtoMember(13)] public readonly float HitPoints;
    [ProtoMember(14)] public readonly float SailHitPoints;
    [ProtoMember(15)] public readonly int RandomValue;
    [ProtoMember(16)] public readonly string CustomSailPatternId;
    /// <summary>The player who fielded an adopted hull; empty when it is the record's owner.</summary>
    [ProtoMember(17)] public readonly string OriginalOwnerControllerId;
    /// <summary>The registry id of the party's campaign ship the hull was built from, so its owner can tell it back on return.</summary>
    [ProtoMember(18)] public readonly string CampaignShipId;

    public BattleShipSpawnData(
        Guid shipId,
        string ownerControllerId,
        string mapEventPartyId,
        bool isNpcParty,
        BattleSideEnum side,
        int formationIndex,
        float[] frame,
        string hullId,
        string[] pieceSlots,
        string[] pieceIds,
        string figureheadId,
        string name,
        float hitPoints,
        float sailHitPoints,
        int randomValue,
        string customSailPatternId,
        string originalOwnerControllerId = null,
        string campaignShipId = null)
    {
        ShipId = shipId;
        OwnerControllerId = ownerControllerId;
        MapEventPartyId = mapEventPartyId;
        IsNpcParty = isNpcParty;
        Side = side;
        FormationIndex = formationIndex;
        Frame = frame;
        HullId = hullId;
        PieceSlots = pieceSlots ?? Array.Empty<string>();
        PieceIds = pieceIds ?? Array.Empty<string>();
        FigureheadId = figureheadId;
        Name = name;
        HitPoints = hitPoints;
        SailHitPoints = sailHitPoints;
        RandomValue = randomValue;
        CustomSailPatternId = customSailPatternId;
        OriginalOwnerControllerId = originalOwnerControllerId == ownerControllerId ? null : originalOwnerControllerId;
        CampaignShipId = campaignShipId;
    }

    /// <summary>The player or host that fielded the hull.</summary>
    public string OriginalOwner => string.IsNullOrEmpty(OriginalOwnerControllerId) ? OwnerControllerId : OriginalOwnerControllerId;
}
