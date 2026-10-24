using Common;
using Common.Messaging;
using GameInterface.Services.AnchorPoints.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.AnchorPoints.Patches;

[HarmonyPatch(typeof(AnchorPoint))]
internal class AnchorPointPatches
{
    [HarmonyPatch(nameof(AnchorPoint.SetPosition))]
    [HarmonyPrefix]
    public static bool SetPositionPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(AnchorPoint.ResetMoveTarget))]
    [HarmonyPrefix]
    public static bool ResetMoveTargetPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(AnchorPoint.SetLastUsedDisembarkPosition))]
    [HarmonyPrefix]
    public static bool SetLastUsedDisembarkPositionPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(AnchorPoint.CallFleet))]
    [HarmonyPrefix]
    public static bool CallFleetPrefix(AnchorPoint __instance, Settlement settlement)
    {
        if (ModInformation.IsServer) return true;

        MessageBroker.Instance.Publish(__instance, new CallFleetRequested(__instance.Owner, settlement));

        return false;
    }

    [HarmonyPatch(nameof(AnchorPoint.OnPartyInteraction))]
    [HarmonyPrefix]
    public static bool OnPartyInteractionPrefix(MobileParty mobileParty)
    {
        if (ModInformation.IsServer) return true;

        // Stop interacting so the request isn't sent every tick until the server's at sea update arrives
        mobileParty.Ai.AiBehaviorInteractable = null;

        MessageBroker.Instance.Publish(mobileParty, new EmbarkAtAnchorRequested(mobileParty));

        return false;
    }

    [HarmonyPatch(nameof(AnchorPoint.HourlyTick))]
    [HarmonyPrefix]
    public static bool HourlyTickPrefix(AnchorPoint __instance, ref bool __state)
    {
        if (ModInformation.IsClient) return false;

        __state = __instance.ArrivalTime != CampaignTime.Zero && __instance.ArrivalTime.IsPast;

        return true;
    }

    [HarmonyPatch(nameof(AnchorPoint.HourlyTick))]
    [HarmonyPostfix]
    public static void HourlyTickPostfix(AnchorPoint __instance, bool __state)
    {
        // Vanilla only notifies MobileParty.MainParty, which the server doesn't have
        if (__state && __instance.Owner.IsPlayerParty())
        {
            MessageBroker.Instance.Publish(__instance, new FleetArrived(__instance.Owner));
        }
    }

    [HarmonyPatch(nameof(AnchorPoint.Position), MethodType.Setter)]
    [HarmonyPostfix]
    public static void PositionSetterPostfix(AnchorPoint __instance)
    {
        if (!IsLocalMainPartyAnchor(__instance)) return;

        // Vanilla SetPosition tracks the main party's fleet on the map, which clients don't run
        var visualTrackerManager = Campaign.Current.VisualTrackerManager;
        if (__instance.IsValid)
        {
            if (!visualTrackerManager.CheckTracked(__instance))
            {
                visualTrackerManager.RegisterObject(__instance);
            }
        }
        else if (visualTrackerManager.CheckTracked(__instance))
        {
            visualTrackerManager.RemoveTrackedObject(__instance, false);
        }

        RefreshCurrentMenu();
    }

    [HarmonyPatch(nameof(AnchorPoint.ArrivalTime), MethodType.Setter)]
    [HarmonyPostfix]
    public static void ArrivalTimeSetterPostfix(AnchorPoint __instance)
    {
        if (!IsLocalMainPartyAnchor(__instance)) return;

        RefreshCurrentMenu();
    }

    private static bool IsLocalMainPartyAnchor(AnchorPoint anchorPoint)
    {
        return ModInformation.IsClient && anchorPoint.Owner != null && anchorPoint.Owner == MobileParty.MainParty;
    }

    // Port menu options like call fleet and set sail read the anchor; MapState.OnActivate refreshes it after a mission
    private static void RefreshCurrentMenu()
    {
        if (TaleWorlds.MountAndBlade.Mission.Current != null) return;

        var menuContext = Campaign.Current.CurrentMenuContext;
        if (menuContext == null) return;

        Campaign.Current.GameMenuManager.RefreshMenuOptions(menuContext);
    }
}

[HarmonyPatch(typeof(MenuContext), nameof(MenuContext.OnHourlyTick))]
internal class AnchorPointMenuRefreshPatch
{
    // Time keeps passing in co-op menus, so the call fleet tooltip's arrival estimate needs refreshing
    [HarmonyPostfix]
    private static void Postfix(MenuContext __instance)
    {
        if (ModInformation.IsServer) return;
        if (TaleWorlds.MountAndBlade.Mission.Current != null) return;

        var anchor = MobileParty.MainParty?.Anchor;
        if (anchor == null || !anchor.IsMovingToPoint) return;

        Campaign.Current.GameMenuManager.RefreshMenuOptions(__instance);
    }
}
