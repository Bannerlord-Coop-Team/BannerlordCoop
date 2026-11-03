using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameMenus.GameMenuInitializationHandlers;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.SiegeEvents.Patches;

/// <summary>
/// The request_meeting menus take their background from PlayerEncounter.EncounterSettlement, which is null once a
/// sally out closed the local encounter while the player is still in the besieged settlement, so the init threw and
/// the menu fell back to the previous one.
/// </summary>
[HarmonyPatch(typeof(DefaultEncounter), nameof(DefaultEncounter.game_menu_town_menu_request_meeting_on_init))]
internal class RequestMeetingMenuBackgroundPatch
{
    [HarmonyPrefix]
    private static bool Prefix(MenuCallbackArgs args)
    {
        if (PlayerEncounter.EncounterSettlement != null) return true;

        var waitMesh = Settlement.CurrentSettlement?.SettlementComponent?.WaitMeshName;
        if (waitMesh != null)
            args.MenuContext.SetBackgroundMeshName(waitMesh);

        return false;
    }
}

/// <summary>
/// The "Forget it" options of the same menus set PlayerEncounter.LeaveEncounter, whose setter writes to
/// PlayerEncounter.Current, so cancelling a meeting after a sally out threw once the menu opened without an encounter.
/// </summary>
[HarmonyPatch(typeof(EncounterGameMenuBehavior))]
internal class RequestMeetingLeavePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_request_meeting_town_leave_on_consequence");
        yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_request_meeting_castle_leave_on_consequence");
    }

    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var setLeaveEncounter = AccessTools.PropertySetter(typeof(PlayerEncounter), nameof(PlayerEncounter.LeaveEncounter));
        var guarded = AccessTools.Method(typeof(RequestMeetingLeavePatch), nameof(SetLeaveEncounterIfOpen));

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(setLeaveEncounter))
                instruction.operand = guarded;
            yield return instruction;
        }
    }

    internal static void SetLeaveEncounterIfOpen(bool value)
    {
        if (PlayerEncounter.Current != null)
            PlayerEncounter.LeaveEncounter = value;
    }
}
