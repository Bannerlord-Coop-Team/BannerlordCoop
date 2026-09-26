using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

public class TroopSacrificeModelPartyPatchTests
{
    [Fact]
    public void InstalledModel_UsesRequestedPartyForBothPlayerBonuses()
    {
        var method = AccessTools.Method(typeof(DefaultTroopSacrificeModel), "GetLostTroopCount");
        var original = PatchProcessor.GetOriginalInstructions(method).ToArray();
        var mainParty = AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.MainParty));
        var playerCharacter = AccessTools.PropertyGetter(typeof(CharacterObject), nameof(CharacterObject.PlayerCharacter));
        Assert.Single(original.Where(instruction => instruction.Calls(mainParty)));
        Assert.Single(original.Where(instruction => instruction.Calls(playerCharacter)));

        var patched = TroopSacrificeModelPartyPatch.Transpiler(original).ToArray();

        Assert.DoesNotContain(patched, instruction => instruction.Calls(mainParty) || instruction.Calls(playerCharacter));
        Assert.Contains(patched, instruction => instruction.Calls(
            AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.LeaderHero))));
        Assert.Contains(patched, instruction => instruction.Calls(
            AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.CharacterObject))));
    }
}
