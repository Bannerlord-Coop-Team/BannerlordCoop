using Common;
using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

[HarmonyPatch(typeof(VillageTradeBoundCampaignBehavior))]
internal class VillageTradeBoundCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(VillageTradeBoundCampaignBehavior.OnNewGameCreated))]
    [HarmonyPrefix]
    private static bool OnNewGameCreatedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(VillageTradeBoundCampaignBehavior.OnSettlementOwnerChanged))]
    [HarmonyPrefix]
    private static bool OnSettlementOwnerChangedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(VillageTradeBoundCampaignBehavior.WarDeclared))]
    [HarmonyPrefix]
    private static bool WarDeclaredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(VillageTradeBoundCampaignBehavior.OnMakePeace))]
    [HarmonyPrefix]
    private static bool OnMakePeacePrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(VillageTradeBoundCampaignBehavior.ClanChangedKingdom))]
    [HarmonyPrefix]
    private static bool ClanChangedKingdomPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(VillageTradeBoundCampaignBehavior.OnClanDestroyed))]
    [HarmonyPrefix]
    private static bool OnClanDestroyedPrefix() => ModInformation.IsServer;
}

// Village._tradeBound isn't a SaveableField so clients need to update it after loading the same way the server does
[HarmonyPatch(typeof(VillageTradeBoundCampaignBehavior), nameof(VillageTradeBoundCampaignBehavior.OnGameLoaded))]
internal class ClientVillageTradeBoundLoadPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        if (ModInformation.IsClient) AllowedThread.AllowThisThread();
    }

    [HarmonyFinalizer]
    private static void Finalizer()
    {
        if (ModInformation.IsClient) AllowedThread.RevokeThisThread();
    }
}
