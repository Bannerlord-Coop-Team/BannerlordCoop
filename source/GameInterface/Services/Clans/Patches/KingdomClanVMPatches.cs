using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Clans;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(KingdomClanVM))]
internal static class KingdomClanVMPatch
{
    public static KingdomClanVM Current { get; private set; }

    [HarmonyPatch(MethodType.Constructor, typeof(Action<KingdomDecision>))]
    [HarmonyPostfix]
    private static void ConstructorPostfix(KingdomClanVM __instance)
    {
        Current = __instance;
    }

    [HarmonyPatch(nameof(KingdomClanVM.OnFinalize))]
    [HarmonyPostfix]
    private static void OnFinalizePostfix(KingdomClanVM __instance)
    {
        if (Current == __instance)
        {
            Current = null;
        }
    }
    [HarmonyPatch(nameof(KingdomClanVM.ExecuteSupport))]
    [HarmonyPrefix]
    private static bool ExecuteSupportPrefix(KingdomClanVM __instance)
    {
        if (Hero.MainHero.Clan.Influence >= (float)__instance.SupportCost)
        {
            __instance.CurrentSelectedClan.Clan.OnSupportedByClan(Hero.MainHero.Clan);
        }
        return false;
    }
}