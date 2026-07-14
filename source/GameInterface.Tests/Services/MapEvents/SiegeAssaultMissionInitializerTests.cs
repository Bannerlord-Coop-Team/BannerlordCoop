using GameInterface.Services.MapEvents;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>
/// Covers the increment-1 battle-type seam: the siege initializer only handles siege-assault events, ranks
/// above the raid and field initializers, and the resolver selects it for a siege event — proven by the
/// initializer's loud-fail guard firing (which runs before any live game state is read) when no
/// <see cref="BattleMissionStartContext"/> wall level is supplied.
///
/// The full record-equality assertion against the legacy inline construction is intentionally out of scope
/// here: that record reads Campaign.Current, LocationComplex, MapSceneWrapper and MobileParty.MainParty and
/// so needs a loaded campaign (covered by the live/E2E path). These tests pin the selection and the
/// snapshot-not-live-state contract, which are what increment 1 introduces.
/// </summary>
public class SiegeAssaultMissionInitializerTests
{
    // MapEvent.IsSiegeAssault reads the private _mapEventType field (== BattleTypes.Siege). The field type
    // is resolved by reflection so the test needs no compile-time reference to the BattleTypes enum.
    private static readonly FieldInfo MapEventTypeField =
        typeof(MapEvent).GetField("_mapEventType", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)!;

    private static MapEvent CreateMapEvent(string battleTypeName)
    {
        MapEvent mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
        object battleType = Enum.Parse(MapEventTypeField.FieldType, battleTypeName);
        MapEventTypeField.SetValue(mapEvent, battleType);
        return mapEvent;
    }

    [Fact]
    public void CanHandle_TrueForSiegeAssault_FalseForFieldBattle()
    {
        var initializer = new SiegeAssaultMissionInitializer();

        Assert.True(initializer.CanHandle(CreateMapEvent("Siege")));
        Assert.False(initializer.CanHandle(CreateMapEvent("FieldBattle")));
    }

    [Fact]
    public void Priority_RanksAboveRaidAndField()
    {
        var siege = new SiegeAssaultMissionInitializer();
        var raid = new VillageRaidMissionInitializer();
        var field = new FieldBattleMissionInitializer();

        Assert.Equal(200, siege.Priority);
        Assert.True(siege.Priority > raid.Priority);
        Assert.True(raid.Priority > field.Priority);
    }

    [Fact]
    public void Create_WithoutContext_FailsLoudlyInsteadOfReadingLiveState()
    {
        var initializer = new SiegeAssaultMissionInitializer();

        var ex = Assert.Throws<InvalidOperationException>(
            () => initializer.Create(CreateMapEvent("Siege"), randomTerrainSeed: 0, atmosphereOnCampaign: default, context: null));

        Assert.Contains("WallLevel", ex.Message);
    }

    [Fact]
    public void Create_WithNullWallLevel_FailsLoudly()
    {
        var initializer = new SiegeAssaultMissionInitializer();

        Assert.Throws<InvalidOperationException>(
            () => initializer.Create(CreateMapEvent("Siege"), randomTerrainSeed: 0, atmosphereOnCampaign: default,
                context: new BattleMissionStartContext(wallLevel: null)));
    }

    [Fact]
    public void Resolver_SelectsSiegeInitializer_ForSiegeAssaultEvent()
    {
        // Give the resolver all three real initializers in a non-priority order; it must sort and pick siege
        // for a siege-assault event. Selection is proven by the siege initializer's snapshot guard throwing
        // (field/raid never build a scene from a BattleMissionStartContext, so this message is siege-specific)
        // — and it throws before touching Campaign.Current, so no loaded game is required.
        var resolver = new BattleMissionInitializerResolver(new IBattleMissionInitializer[]
        {
            new FieldBattleMissionInitializer(),
            new VillageRaidMissionInitializer(),
            new SiegeAssaultMissionInitializer(),
        });

        var ex = Assert.Throws<InvalidOperationException>(
            () => resolver.Create(CreateMapEvent("Siege"), randomTerrainSeed: 0, atmosphereOnCampaign: default, context: null));

        Assert.Contains("WallLevel", ex.Message);
    }
}
