using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Missions.Naval;

/// <summary>
/// Mission-side copy of a campaign ship. Vanilla <c>Ship.OnShipDamaged</c> writes campaign hit points mid-mission,
/// so coop missions hand this snapshot to <c>NavalShipsLogic</c> instead and keep all damage mission-local.
/// </summary>
public sealed class CoopShipOrigin : IShipOrigin
{
    private readonly List<ShipVisualSlotInfo> visualSlots;
    private readonly List<ShipSlotAndPieceName> slotPieces;

    public CoopShipOrigin(IShipOrigin source)
    {
        Source = source;
        Hull = source.Hull;
        Name = source.Name;
        OriginShipId = source.OriginShipId;
        IsPlayerShip = source.IsPlayerShip;
        HitPoints = source.HitPoints;
        MaxHitPoints = source.MaxHitPoints;
        MaxFireHitPoints = source.MaxFireHitPoints;
        SailHitPoints = source.SailHitPoints;
        MaxSailHitPoints = source.MaxSailHitPoints;
        TotalCrewCapacity = source.TotalCrewCapacity;
        MainDeckCrewCapacity = source.MainDeckCrewCapacity;
        SkeletalCrewCapacity = source.SkeletalCrewCapacity;
        DefaultFormationGroupIndex = source.DefaultFormationGroupIndex;
        ForwardDragFactor = source.ForwardDragFactor;
        ShipWeightFactor = source.ShipWeightFactor;
        RudderSurfaceAreaFactor = source.RudderSurfaceAreaFactor;
        RandomValue = source.RandomValue;
        CustomSailPatternId = source.CustomSailPatternId;
        MaxRudderForceFactor = source.MaxRudderForceFactor;
        MaxOarForceFactor = source.MaxOarForceFactor;
        SailForceFactor = source.SailForceFactor;
        MaxOarPowerFactor = source.MaxOarPowerFactor;
        SailRotationSpeedFactor = source.SailRotationSpeedFactor;
        FurlUnfurlSpeedFactor = source.FurlUnfurlSpeedFactor;
        CrewShieldHitPointsFactor = source.CrewShieldHitPointsFactor;
        CrewMeleeDamageFactor = source.CrewMeleeDamageFactor;
        AdditionalArcherQuivers = source.AdditionalArcherQuivers;
        AdditionalThrowingWeaponStack = source.AdditionalThrowingWeaponStack;
        visualSlots = source.GetShipVisualSlotInfos();
        slotPieces = source.GetShipSlotAndPieceNames();
    }

    /// <summary>The campaign ship this copy was taken from. Read-only in the mission.</summary>
    public IShipOrigin Source { get; }

    public ShipHull Hull { get; }
    public TextObject Name { get; }
    public string OriginShipId { get; }
    public bool IsPlayerShip { get; }
    public float HitPoints { get; private set; }
    public float MaxHitPoints { get; }
    public float MaxFireHitPoints { get; }
    public float SailHitPoints { get; private set; }
    public float MaxSailHitPoints { get; }
    public int TotalCrewCapacity { get; }
    public int MainDeckCrewCapacity { get; }
    public int SkeletalCrewCapacity { get; }
    public int DefaultFormationGroupIndex { get; }
    public float ForwardDragFactor { get; }
    public float ShipWeightFactor { get; }
    public float RudderSurfaceAreaFactor { get; }
    public int RandomValue { get; }
    public string CustomSailPatternId { get; }
    public float MaxRudderForceFactor { get; }
    public float MaxOarForceFactor { get; }
    public float SailForceFactor { get; }
    public float MaxOarPowerFactor { get; }
    public float SailRotationSpeedFactor { get; }
    public float FurlUnfurlSpeedFactor { get; }
    public float CrewShieldHitPointsFactor { get; }
    public float CrewMeleeDamageFactor { get; }
    public int AdditionalArcherQuivers { get; }
    public int AdditionalThrowingWeaponStack { get; }

    public void OnShipDamaged(float rawDamage, IShipOrigin rammingShip, out float modifiedDamage)
    {
        modifiedDamage = 0f;
        HitPoints = MathF.Max(0f, HitPoints - rawDamage);
    }

    public void OnSailDamaged(float rawDamage, float inflictedDamage)
    {
        SailHitPoints = MathF.Max(0f, SailHitPoints - inflictedDamage);
    }

    public List<ShipVisualSlotInfo> GetShipVisualSlotInfos() => new List<ShipVisualSlotInfo>(visualSlots);

    public List<ShipSlotAndPieceName> GetShipSlotAndPieceNames() => new List<ShipSlotAndPieceName>(slotPieces);
}
