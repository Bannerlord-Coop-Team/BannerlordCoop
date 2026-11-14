using Common;
using Common.Messaging;
using GameInterface.Services.AnchorPoints.Messages;
using GameInterface.Services.GameMenus;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.AnchorPoints.Patches;

[HarmonyPatch(typeof(AnchorPoint))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class AnchorPointPatches
{
    // v1.5 has no SetPosition or ResetMoveTarget: the Position and Settlement setters and ResetPosition move the anchor
    [HarmonyPatch(nameof(AnchorPoint.Position), MethodType.Setter)]
    [HarmonyPrefix]
    public static bool PositionSetterPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(AnchorPoint.Settlement), MethodType.Setter)]
    [HarmonyPrefix]
    public static bool SettlementSetterPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(AnchorPoint.ResetPosition))]
    [HarmonyPrefix]
    public static bool ResetPositionPrefix() => ModInformation.IsServer;

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

    // Campaign.Tick calls this for MobileParty.MainParty only, so the server never runs it for a player's fleet.
    // A client keeps the synced ArrivalTime, tells its player once when the fleet arrives and tracks the fleet on the map,
    // which vanilla does in the Position and Settlement setters that only run on the server.
    [HarmonyPatch(nameof(AnchorPoint.TryUpdatePlayerAnchorInfo))]
    [HarmonyPrefix]
    public static bool TryUpdatePlayerAnchorInfoPrefix(AnchorPoint __instance)
    {
        if (ModInformation.IsServer) return true;

        if (__instance.ArrivalTime != CampaignTime.Zero && __instance.ArrivalTime.IsPast && !IsArrivalShown(__instance))
        {
            ArrivalsShown.Remove(__instance);
            ArrivalsShown.Add(__instance, new StrongBox<CampaignTime>(__instance.ArrivalTime));
            InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=qFAhDtrw}Your fleet has arrived at its destination.").ToString(), new Color(0f, 1f, 0f))
            {
                SoundEventPath = "event:/ui/panels/panel_call_fleet"
            });
            RefreshCurrentMenu();
        }

        __instance.UpdateAnchorVisualTrackers();

        return false;
    }

    private static readonly ConditionalWeakTable<AnchorPoint, StrongBox<CampaignTime>> ArrivalsShown = new ConditionalWeakTable<AnchorPoint, StrongBox<CampaignTime>>();

    private static bool IsArrivalShown(AnchorPoint anchorPoint) =>
        ArrivalsShown.TryGetValue(anchorPoint, out var shown) && shown.Value == anchorPoint.ArrivalTime;

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

    // Port menu options like call fleet and set sail read the anchor
    private static void RefreshCurrentMenu()
    {
        if (!ContainerProvider.TryResolve<IMenuOptionsRefresher>(out var refresher)) return;

        refresher.RefreshMenuOptions(Campaign.Current?.CurrentMenuContext);
    }
}

[HarmonyPatch(typeof(MenuContext), nameof(MenuContext.OnHourlyTick))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class AnchorPointMenuRefreshPatch
{
    // Time keeps passing in co-op menus, so the call fleet tooltip's arrival estimate needs refreshing
    [HarmonyPostfix]
    internal static void Postfix(MenuContext __instance)
    {
        if (ModInformation.IsServer) return;

        var anchor = MobileParty.MainParty?.Anchor;
        if (anchor == null || !anchor.IsMovingToPoint) return;
        if (!ContainerProvider.TryResolve<IMenuOptionsRefresher>(out var refresher)) return;

        refresher.RefreshMenuOptions(__instance);
    }
}
