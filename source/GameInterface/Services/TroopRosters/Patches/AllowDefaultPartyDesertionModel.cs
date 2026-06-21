using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameComponents;

[HarmonyPatch(typeof(DefaultPartyDesertionModel))]
internal class AllowDefaultPartyDesertionModel
{
    [HarmonyPatch(nameof(DefaultPartyDesertionModel.GetTroopsToDesert))]
    [HarmonyPrefix]
    private static void PrefixTick()
    {
        AllowedThread.AllowThisThread();
    }

    [HarmonyPatch(nameof(DefaultPartyDesertionModel.GetTroopsToDesert))]
    [HarmonyFinalizer]
    private static void Finalizer_Tick()
    {
        AllowedThread.RevokeThisThread();
    }
}