using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.SiegeEvents.Patches;

[HarmonyPatch(typeof(DefaultTroopSacrificeModel), "GetLostTroopCount")]
internal class TroopSacrificeModelPartyPatch
{
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var mainParty = AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.MainParty));
        var playerCharacter = AccessTools.PropertyGetter(typeof(CharacterObject), nameof(CharacterObject.PlayerCharacter));
        var leader = AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.LeaderHero));
        var character = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.CharacterObject));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(mainParty) || instruction.Calls(playerCharacter))
            {
                // The dedicated server has no main party; use the party whose losses are being quoted.
                yield return new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                if (instruction.Calls(playerCharacter))
                {
                    yield return new CodeInstruction(OpCodes.Callvirt, leader);
                    yield return new CodeInstruction(OpCodes.Callvirt, character);
                }
            }
            else yield return instruction;
        }
    }
}
