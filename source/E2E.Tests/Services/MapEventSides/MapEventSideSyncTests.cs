using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEventSides;
public class MapEventSideSyncTests : SyncTestBase
{
    public MapEventSideSyncTests(ITestOutputHelper output) : base(output)
    {
        TestEnvironment.CreateRegisteredObject<MapEventSide>();
        TestEnvironment.CreateRegisteredObject<MapEvent>();
        TestEnvironment.CreateRegisteredObject<CharacterObject>();
    }

    [Fact]
    public void Server_MapEventSide_Sync()
    {
        TestEnvironment.AssertField<MapEventSide, int>(nameof(MapEventSide.Casualties), 5);
        TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.InfluenceValue), 5f);
        TestEnvironment.AssertField<MapEventSide, bool>(nameof(MapEventSide.IsSurrendered), true);
        TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.LeaderSimulationModifier), 5f);
        TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.RenownAtMapEventEnd), 5f);
        TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.RenownValue), 5f);
        TestEnvironment.AssertField<MapEventSide, float>(nameof(MapEventSide.StrengthRatio), 5f);
        //TestEnvironment.AssertReferenceField<MapEventSide, MapEvent>(nameof(MapEventSide._mapEvent));
        TestEnvironment.AssertField<MapEventSide, bool>(nameof(MapEventSide._requiresTroopCacheUpdate), true);
        TestEnvironment.AssertReferenceField<MapEventSide, CharacterObject>(nameof(MapEventSide._selectedSimulationTroop));
        TestEnvironment.AssertField<MapEventSide, int>(nameof(MapEventSide._selectedSimulationTroopIndex), 5);
    }
}
