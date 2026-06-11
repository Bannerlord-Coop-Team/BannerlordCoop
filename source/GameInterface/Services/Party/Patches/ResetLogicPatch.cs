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

    static void Postfix()
    {
        AllowedThread.RevokeThisThread();
    }
}
