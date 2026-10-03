using Missions.Naval;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class CoopShipOriginTests
{
    [Fact]
    public void OnShipDamaged_LowersMissionHitPointsWithoutWritingTheCampaignShip()
    {
        var campaignShip = new CampaignShipStub();
        var origin = new CoopShipOrigin(campaignShip);

        origin.OnShipDamaged(30f, null, out var modifiedDamage);

        Assert.Equal(70f, origin.HitPoints);
        Assert.Equal(0f, modifiedDamage);
        Assert.Equal(100f, campaignShip.HitPoints);
        Assert.Equal(0, campaignShip.DamageCalls);
    }

    [Fact]
    public void OnSailDamaged_LowersMissionSailHitPointsWithoutWritingTheCampaignShip()
    {
        var campaignShip = new CampaignShipStub();
        var origin = new CoopShipOrigin(campaignShip);

        origin.OnSailDamaged(50f, 20f);

        Assert.Equal(30f, origin.SailHitPoints);
        Assert.Equal(50f, campaignShip.SailHitPoints);
        Assert.Equal(0, campaignShip.DamageCalls);
    }

    private sealed class CampaignShipStub : IShipOrigin
    {
        public int DamageCalls { get; private set; }
        public ShipHull Hull => null;
        public TextObject Name => null;
        public string OriginShipId => "nord_medium_ship";
        public bool IsPlayerShip => true;
        public float HitPoints { get; private set; } = 100f;
        public float MaxHitPoints => 100f;
        public float MaxFireHitPoints => 10f;
        public float SailHitPoints { get; private set; } = 50f;
        public float MaxSailHitPoints => 50f;
        public int TotalCrewCapacity => 30;
        public int MainDeckCrewCapacity => 20;
        public int SkeletalCrewCapacity => 10;
        public int DefaultFormationGroupIndex => 0;
        public float ForwardDragFactor => 1f;
        public float ShipWeightFactor => 1f;
        public float RudderSurfaceAreaFactor => 1f;
        public int RandomValue => 42;
        public string CustomSailPatternId => "";
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
            DamageCalls++;
            HitPoints -= rawDamage;
            modifiedDamage = 0f;
        }

        public void OnSailDamaged(float rawDamage, float inflictedDamage)
        {
            DamageCalls++;
            SailHitPoints -= inflictedDamage;
        }

        public List<ShipVisualSlotInfo> GetShipVisualSlotInfos() => new List<ShipVisualSlotInfo>();

        public List<ShipSlotAndPieceName> GetShipSlotAndPieceNames() => new List<ShipSlotAndPieceName>();
    }
}
