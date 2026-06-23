using Common.Util;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace GameInterface.Services.ItemRosters.Patches;

[HarmonyPatch]
internal class AllowItemRostersInGUI
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(PropertyBasedTooltipVM), "OnPeriodicRefresh"),
        AccessTools.Method(typeof(GauntletInformationView), "OnShowTooltip"),
        AccessTools.Method(typeof(TooltipRefresherCollection), "RefreshSettlementTooltip"),
        AccessTools.Method(typeof(PartyScreenHelper), nameof(PartyScreenHelper.OpenPartyScreen))
    };

    [HarmonyPrefix]
    private static void Prefix()
    {
        AllowedThread.AllowThisThread();
    }

    // Finalizers (not postfixes) so the revoke runs even when the original throws;
    // a skipped revoke would leave the thread permanently allowed.
    [HarmonyFinalizer]
    private static void Finalizer()
    {
        AllowedThread.RevokeThisThread();
    }
}