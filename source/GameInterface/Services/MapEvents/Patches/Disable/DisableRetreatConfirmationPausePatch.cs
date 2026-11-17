using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;

namespace GameInterface.Services.MapEvents.Patches.Disable;

// A coop battle keeps running for everyone else, so the retreat confirmation shows without pausing the engine.
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
        InformationManager.ShowInquiry(__instance.GetRetreatPopUpData());
        __instance.IsWarningWidgetOpened = true;
        return false;
    }
}
