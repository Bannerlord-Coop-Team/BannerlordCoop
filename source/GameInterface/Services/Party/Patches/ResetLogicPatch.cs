using Common.Util;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch]
internal class ResetLogicPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(PartyScreenLogic), nameof(PartyScreenLogic.ResetLogic)),
    };

    static void Prefix()
    {
        AllowedThread.AllowThisThread();
    }

    // Finalizer (not postfix) so the revoke runs even when the original throws;
    // a skipped revoke would leave the thread permanently allowed.
    static void Finalizer()
    {
        AllowedThread.RevokeThisThread();
    }
}
