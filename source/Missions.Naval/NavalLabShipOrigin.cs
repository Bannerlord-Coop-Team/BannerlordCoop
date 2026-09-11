#if DEBUG
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Missions.Naval;

internal sealed class NavalLabShipOrigin : IShipOrigin
{
    private readonly Action<string> reject;
    public NavalLabShipOrigin(ShipHull hull, Action<string> reject)
    {
        Hull = hull;
        this.reject = reject;
    }
    public ShipHull Hull { get; }
    public TextObject Name => Hull.Name;
    public string OriginShipId => Hull.MissionShipObjectId;
    public bool IsPlayerShip => false;
    public float HitPoints => MaxHitPoints;
    public float MaxHitPoints => Hull.MaxHitPoints;
    public float MaxFireHitPoints => Hull.MaxFireHitPoints;
    public float SailHitPoints => MaxSailHitPoints;
    public float MaxSailHitPoints => Hull.MaxSailHitPoints;
    public int TotalCrewCapacity => Hull.TotalCrewCapacity;
    public int MainDeckCrewCapacity => Hull.MainDeckCrewCapacity;
    public int SkeletalCrewCapacity => Hull.SkeletalCrewCapacity;
    public int DefaultFormationGroupIndex => 0;
    public float ForwardDragFactor => 1f;
    public float ShipWeightFactor => 1f;
    public float RudderSurfaceAreaFactor => 1f;
    public int RandomValue => 3536;
    public string CustomSailPatternId => null;
    public float MaxRudderForceFactor => 1f;
    public float MaxOarForceFactor => 1f;
    public float SailForceFactor => 1f;
    public float MaxOarPowerFactor => 1f;
    public float SailRotationSpeedFactor => 1f;
    public float FurlUnfurlSpeedFactor => 1f;
    public float CrewShieldHitPointsFactor => 1f;
    public float CrewMeleeDamageFactor => 1f;
    public int AdditionalArcherQuivers => 0;
    public int AdditionalThrowingWeaponStack => 0;
    public void OnShipDamaged(float rawDamage, IShipOrigin rammingShip, out float modifiedDamage)
    {
        modifiedDamage = 0;
        reject("ship.damage");
    }
    public void OnSailDamaged(float rawDamage, float inflictedDamage) => reject("ship.sailDamage");
    public List<ShipVisualSlotInfo> GetShipVisualSlotInfos() => new List<ShipVisualSlotInfo>();
    public List<ShipSlotAndPieceName> GetShipSlotAndPieceNames() => new List<ShipSlotAndPieceName>();
}
#endif
