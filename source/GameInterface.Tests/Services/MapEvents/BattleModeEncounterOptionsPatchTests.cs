using Common.Util;
using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Verifies destroyed battle cleanup cannot be blocked by the abandon-army menu condition.</summary>
public class BattleModeEncounterOptionsPatchTests
{
    [Fact]
    public void HarmonyPatch_IncludesAbandonArmyCondition()
    {
        var harmony = new Harmony(nameof(HarmonyPatch_IncludesAbandonArmyCondition));
        try
        {
            var patched = harmony.CreateClassProcessor(typeof(BattleModeEncounterOptionsPatch)).Patch();

            Assert.Contains(
                patched,
                method => method.Name.Contains("game_menu_encounter_abandon_army_on_condition"));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void ShouldSkipAbandonArmyCondition_ArmyFollowerWithoutMapEvent_ReturnsTrue()
    {
        var follower = CreateParty();
        var leader = CreateParty();
        var army = ObjectHelper.SkipConstructor<Army>();
        army.LeaderParty = leader;
        follower._army = army;

        Assert.True(BattleModeEncounterOptionsPatch.ShouldSkipAbandonArmyCondition(follower));
    }

    [Fact]
    public void ShouldSkipAbandonArmyCondition_ArmyFollowerWithMapEvent_ReturnsFalse()
    {
        var follower = CreateParty();
        var leader = CreateParty();
        var army = ObjectHelper.SkipConstructor<Army>();
        army.LeaderParty = leader;
        follower._army = army;
        follower.Party._mapEventSide = new MapEventSide(
            ObjectHelper.SkipConstructor<MapEvent>(),
            BattleSideEnum.Attacker,
            follower.Party);

        Assert.False(BattleModeEncounterOptionsPatch.ShouldSkipAbandonArmyCondition(follower));
    }

    [Fact]
    public void ShouldSkipAbandonArmyCondition_ArmyLeaderWithoutMapEvent_ReturnsFalse()
    {
        var leader = CreateParty();
        var army = ObjectHelper.SkipConstructor<Army>();
        army.LeaderParty = leader;
        leader._army = army;

        Assert.False(BattleModeEncounterOptionsPatch.ShouldSkipAbandonArmyCondition(leader));
    }

    private static MobileParty CreateParty()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Party = ObjectHelper.SkipConstructor<PartyBase>();
        return party;
    }
}
