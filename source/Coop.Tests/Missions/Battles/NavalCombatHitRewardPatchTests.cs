using HarmonyLib;
using Missions.Naval;
using NavalDLC.CharacterDevelopment;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class NavalCombatHitRewardPatchTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData(null, null, false)]
    public void IsNavalHit_UsesTheLocalMissionElseTheAttackersMapEvent(bool? missionIsNaval, bool? mapEventIsNaval, bool expected)
    {
        Assert.Equal(expected, NavalCombatHitRewardPatch.IsNavalHit(missionIsNaval, mapEventIsNaval));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void IsNavalHit_OnTheServer_ResolvesTheAttackerHerosPartyMapEvent(bool isOnLand, bool expected)
    {
        Assert.Null(Mission.Current);

        Assert.Equal(expected, NavalCombatHitRewardPatch.IsNavalHit(HeroInMapEvent(isOnLand)));
    }

    [Fact]
    public void IsNavalHit_OnTheServer_WithoutAnAttackerParty_IsNotNaval()
    {
        var character = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
        character._heroObject = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

        Assert.False(NavalCombatHitRewardPatch.IsNavalHit(character));
        Assert.False(NavalCombatHitRewardPatch.IsNavalHit((CharacterObject)null));
    }

    [Fact]
    public void Transpiler_ReplacesTheMissionNavalCheckWithTheAttackerResolution()
    {
        var original = PatchProcessor.GetOriginalInstructions(
            AccessTools.DeclaredMethod(typeof(NavalSkillLevellingManager), nameof(NavalSkillLevellingManager.OnCombatHit)));
        var resolver = AccessTools.DeclaredMethod(typeof(NavalCombatHitRewardPatch), nameof(NavalCombatHitRewardPatch.IsNavalHit),
            new[] { typeof(CharacterObject) });

        var instrumented = NavalCombatHitRewardPatch.Transpiler(original).ToList();

        Assert.DoesNotContain(instrumented, code => code.Calls(AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.Current))));
        int call = instrumented.FindIndex(code => code.Calls(resolver));
        Assert.True(call > 0);
        Assert.Equal(OpCodes.Ldarg_1, instrumented[call - 1].opcode);
        Assert.Equal(original.Count, instrumented.Count);
    }

    [Fact]
    public void Patch_BindsToTheInstalledNavalDlc()
    {
        var harmony = new Harmony("coop.tests.naval.combat_hit_reward");
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(NavalCombatHitRewardPatch)).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static CharacterObject HeroInMapEvent(bool isOnLand)
    {
        var mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
        mapEvent.Position = new CampaignVec2(new Vec2(10f, 20f), isOnLand);
        var side = (MapEventSide)FormatterServices.GetUninitializedObject(typeof(MapEventSide));
        AccessTools.Field(typeof(MapEventSide), "_mapEvent").SetValue(side, mapEvent);
        var partyBase = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
        partyBase._mapEventSide = side;
        var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        party.Party = partyBase;
        var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
        hero._partyBelongedTo = party;
        var character = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
        character._heroObject = hero;
        return character;
    }
}
