using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEventComponents;

public class RaidEventComponentTests : SyncTestBase
{
    public RaidEventComponentTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Server_RaidEventComponent_ProgressFields()
    {
        string? componentId = null;
        float defaultRaidDamage = 0;
        float defaultNextSettlementDamage = 0;
        int defaultLootedItemCount = 0;
        bool defaultMilitiaResistanceFight = false;

        TestEnvironment.Server.Call(() =>
        {
            var mapEvent = new MapEvent();
            var component = new RaidEventComponent(mapEvent);

            defaultRaidDamage = component.RaidDamage;
            defaultNextSettlementDamage = component._nextSettlementDamage;
            defaultLootedItemCount = component._lootedItemCount;
            defaultMilitiaResistanceFight = component._isMilitiaResistanceFight;

            Assert.True(Server.ObjectManager.TryGetId(component, out componentId));
        });

        Assert.NotNull(componentId);

        TestEnvironment.AssertProperty<RaidEventComponent, float>(
            nameof(RaidEventComponent.RaidDamage),
            0.42f,
            defaultRaidDamage,
            componentId);
        TestEnvironment.AssertField<RaidEventComponent, float>(
            "<RaidDamage>k__BackingField",
            0.73f,
            componentId,
            0.42f);
        TestEnvironment.AssertField<RaidEventComponent, float>(
            nameof(RaidEventComponent._nextSettlementDamage),
            0.08f,
            componentId,
            defaultNextSettlementDamage);
        TestEnvironment.AssertField<RaidEventComponent, int>(
            nameof(RaidEventComponent._lootedItemCount),
            3,
            componentId,
            defaultLootedItemCount);
        TestEnvironment.AssertField<RaidEventComponent, bool>(
            nameof(RaidEventComponent._isMilitiaResistanceFight),
            true,
            componentId,
            defaultMilitiaResistanceFight);
    }

    [Fact]
    public void Server_RaidEventComponent_ProductionRewardsDictionaryIsCreated()
    {
        TestEnvironment.Server.Call(() =>
        {
            var mapEvent = new MapEvent();
            var component = new RaidEventComponent(mapEvent);

            Assert.NotNull(component._raidProductionRewards);
        });
    }
}