using Common.Util;
using GameInterface.Services.MapEventComponents.Patches;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.MapEventComponents;

public class RaidEventComponentPatchesTests
{
    private readonly ItemObject horse;
    private readonly ItemModifier lame;

    public RaidEventComponentPatchesTests()
    {
        horse = ObjectHelper.SkipConstructor<ItemObject>();
        horse.StringId = "sumpter_horse";
        lame = ObjectHelper.SkipConstructor<ItemModifier>();
        lame.StringId = "lame_horse";
    }

    [Fact]
    public void UnchangedRosterWithTwoStacksOfOneItemReportsNoLoot()
    {
        var roster = new ItemRoster();
        roster.AddToCounts(new EquipmentElement(horse), 100);
        roster.AddToCounts(new EquipmentElement(horse, lame), 2);

        var before = RaidEventComponentPatches.CaptureItems(roster);

        Assert.Equal(0, RaidEventComponentPatches.GetAddedItems(before, roster).Count);
    }

    [Fact]
    public void LootIsReportedOnlyForTheStackThatGrew()
    {
        var roster = new ItemRoster();
        roster.AddToCounts(new EquipmentElement(horse), 100);
        roster.AddToCounts(new EquipmentElement(horse, lame), 2);

        var before = RaidEventComponentPatches.CaptureItems(roster);
        roster.AddToCounts(new EquipmentElement(horse), 5);

        var looted = Assert.Single(RaidEventComponentPatches.GetAddedItems(before, roster));
        Assert.Null(looted.EquipmentElement.ItemModifier);
        Assert.Equal(5, looted.Amount);
    }
}
