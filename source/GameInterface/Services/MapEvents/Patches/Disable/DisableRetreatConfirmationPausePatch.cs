using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;

namespace GameInterface.Services.MapEvents.Patches.Disable;

[HarmonyPatch(typeof(BasicMissionHandler), nameof(BasicMissionHandler.CreateWarningWidgetForResult))]
internal class DisableRetreatConfirmationPausePatch
{
    [HarmonyPrefix]
    private static bool Prefix(BasicMissionHandler __instance, BattleEndLogic.ExitResult result)
    {
        if (result != BattleEndLogic.ExitResult.NeedsPlayerConfirmation)
        {
            return true;
        }

        __instance._isSurrender = false;
        InformationManager.ShowInquiry(__instance.GetRetreatPopUpData(), false, false);
        return false;
    }
}
