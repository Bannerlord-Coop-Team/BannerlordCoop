using Common.Util;
using HarmonyLib;
using Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace GameInterface.Services.ItemRosters.Patches;

[HarmonyPatch]
internal class AllowTroopRostersInGUI
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(TooltipBaseVM), nameof(TooltipBaseVM.Tick)),
        AccessTools.Method(typeof(TooltipProperty), nameof(TooltipProperty.RefreshDefinition)),
        AccessTools.Method(typeof(PartyScreenLogic), nameof(PartyScreenLogic.RemoveZeroCounts)),
        AccessTools.Method(typeof(PartyCharacterVM), nameof(PartyCharacterVM.ApplyTransfer))
    }
    .Concat(AccessTools.GetDeclaredMethods(typeof(TooltipRefresherCollection)).Where(method => method.Name.Contains("Refresh")));

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