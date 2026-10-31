using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.NavalDLC;

/// <summary>
/// Ships are saved by value because an ownerless ship has no registry id after a reload.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class PlayerCachedFleetData
{
    [ProtoMember(1)]
    public readonly List<CachedShipData> Ships;

    [ProtoMember(2)]
    public readonly float AnchorX;

    [ProtoMember(3)]
    public readonly float AnchorY;

    [ProtoMember(4)]
    public readonly bool AnchorIsOnLand;

    [ProtoMember(5)]
    public readonly float DisembarkX;

    [ProtoMember(6)]
    public readonly float DisembarkY;

    [ProtoMember(7)]
    public readonly bool DisembarkIsOnLand;

    public PlayerCachedFleetData(
        List<CachedShipData> ships,
        float anchorX,
        float anchorY,
        bool anchorIsOnLand,
        float disembarkX,
        float disembarkY,
        bool disembarkIsOnLand)
    {
        Ships = ships;
        AnchorX = anchorX;
        AnchorY = anchorY;
        AnchorIsOnLand = anchorIsOnLand;
        DisembarkX = disembarkX;
        DisembarkY = disembarkY;
        DisembarkIsOnLand = disembarkIsOnLand;
    }
}

[ProtoContract(SkipConstructor = true)]
public class CachedShipData
{
    [ProtoMember(1)]
    public readonly string HullId;

    [ProtoMember(2)]
    public readonly string Name;

    [ProtoMember(3)]
    public readonly float HitPoints;

    [ProtoMember(4)]
    public readonly float SailHitPoints;

    [ProtoMember(5)]
    public readonly string FigureheadId;

    [ProtoMember(6)]
    public readonly string CustomSailPatternId;

    [ProtoMember(7)]
    public readonly int RandomValue;

    // Dictionary<SlotTag, ShipUpgradePieceId>
    [ProtoMember(8)]
    public readonly Dictionary<string, string> SlotPieceIds;

    [ProtoMember(9)]
    public readonly List<string> UnlockedPieceIds;

    public CachedShipData(
        string hullId,
        string name,
        float hitPoints,
        float sailHitPoints,
        string figureheadId,
        string customSailPatternId,
        int randomValue,
        Dictionary<string, string> slotPieceIds,
        List<string> unlockedPieceIds)
    {
        HullId = hullId;
        Name = name;
        HitPoints = hitPoints;
        SailHitPoints = sailHitPoints;
        FigureheadId = figureheadId;
        CustomSailPatternId = customSailPatternId;
        RandomValue = randomValue;
        SlotPieceIds = slotPieceIds;
        UnlockedPieceIds = unlockedPieceIds;
    }
}
