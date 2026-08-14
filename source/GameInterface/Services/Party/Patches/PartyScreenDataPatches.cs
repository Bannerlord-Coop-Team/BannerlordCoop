using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch(typeof(PartyScreenData))]
internal class PartyScreenDataPatches
{
    [HarmonyPatch(nameof(PartyScreenData.ResetUsing))]
    [HarmonyPrefix]
    public static void ResetUsingPrefix()
    {
        AllowedThread.AllowThisThread();
    }

    [HarmonyPatch(nameof(PartyScreenData.ResetUsing))]
    [HarmonyFinalizer]
    public static void ResetUsingFinalizer()
    {
        AllowedThread.RevokeThisThread();
    }
}
