using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;

namespace GameInterface.Services.MapEvents.Patches.Disable;

[HarmonyPatch(typeof(BasicMissionHandler), nameof(BasicMissionHandler.CreateWarningWidgetForResult))]
internal class DisableRetreatConfirmationPausePatch
{
    internal const string NavalRetreatDisabledMessage = "Retreat is disabled in naval battles.";

    [HarmonyPrefix]
    private static bool Prefix(BasicMissionHandler __instance, BattleEndLogic.ExitResult result)
    {
        // Naval retreat would leave this player's hull and crew in every peer's mission; it is not supported yet.
        if (IsRetreatDisabled(MobileParty.MainParty?.MapEvent))
        {
            MessageBroker.Instance.Publish(__instance, new SendInformationMessage(NavalRetreatDisabledMessage));
            return false;
        }

        if (result != BattleEndLogic.ExitResult.NeedsPlayerConfirmation)
        {
            return true;
        }

        __instance._isSurrender = false;
        InformationManager.ShowInquiry(__instance.GetRetreatPopUpData());
        __instance.IsWarningWidgetOpened = true;
        return false;
    }

    internal static bool IsRetreatDisabled(MapEvent mapEvent)
    {
        return mapEvent?.IsNavalMapEvent == true;
    }
}
