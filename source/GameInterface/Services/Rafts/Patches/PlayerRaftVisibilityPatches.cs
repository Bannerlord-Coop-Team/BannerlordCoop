using Common.Logging;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using SandBox.ViewModelCollection.Nameplate;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace GameInterface.Services.Rafts.Patches;

/// <summary>
/// Other players' rafts stay visible with their nameplate but can't be engaged, so a player sailing back
/// after escaping captivity can be seen without being attacked or talked to.
/// </summary>
[HarmonyPatch]
internal class PlayerRaftVisibilityPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerRaftVisibilityPatches>();

    private static readonly MethodInfo IsInRaftStateGetter = AccessTools.PropertyGetter(typeof(MobileParty), nameof(MobileParty.IsInRaftState));
    private static readonly MethodInfo HidesRaftNameplateMethod = AccessTools.Method(typeof(PlayerRaftVisibilityPatches), nameof(HidesRaftNameplate));

    // Vanilla hides the nameplate of every raft that isn't the main party
    [HarmonyPatch(typeof(PartyNameplateVM), nameof(PartyNameplateVM.RefreshDynamicProperties))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> RefreshDynamicPropertiesTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var replaced = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(IsInRaftStateGetter))
            {
                replaced++;
                yield return new CodeInstruction(OpCodes.Call, HidesRaftNameplateMethod).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                continue;
            }

            yield return instruction;
        }

        if (replaced != 1)
            Logger.Error("Expected one IsInRaftState read in {Method}, found {Count}", nameof(PartyNameplateVM.RefreshDynamicProperties), replaced);
    }

    private static bool HidesRaftNameplate(MobileParty party) => party.IsInRaftState && !party.IsPlayerParty();

    [HarmonyPatch(typeof(PartyBase), "TaleWorlds.CampaignSystem.Map.IInteractablePoint.CanPartyInteract")]
    [HarmonyPostfix]
    private static void CanPartyInteractPostfix(PartyBase __instance, MobileParty mobileParty, ref bool __result)
    {
        if (!__result) return;

        if (IsOtherPlayersRaft(__instance.MobileParty, mobileParty))
            __result = false;
    }

    // A click on another player's raft moves next to it instead of following it forever
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.SetMoveEngageParty))]
    [HarmonyPrefix]
    private static bool SetMoveEngagePartyPrefix(MobileParty __instance, MobileParty party, NavigationType navigationType)
    {
        if (!IsOtherPlayersRaft(party, __instance)) return true;

        __instance.SetMoveGoToPoint(party.Position, navigationType);
        return false;
    }

    private static bool IsOtherPlayersRaft(MobileParty target, MobileParty interactor) =>
        target != null && target != interactor && target.IsInRaftState && target.IsPlayerParty();
}
