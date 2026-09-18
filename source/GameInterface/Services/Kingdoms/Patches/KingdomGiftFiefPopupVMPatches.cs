using Common;
using Common.Messaging;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement;

namespace GameInterface.Services.Kingdoms.Patches;

[HarmonyPatch(typeof(KingdomGiftFiefPopupVM))]
internal class KingdomGiftFiefPopupVMPatches
{
    /// <summary>
    /// Intercepts the client fief gift logic
    /// </summary>
    [HarmonyPatch(nameof(KingdomGiftFiefPopupVM.ExecuteGiftSettlement))]
    [HarmonyPrefix]
    private static bool ExecuteGiftSettlementPrefix(KingdomGiftFiefPopupVM __instance)
    {
        if (__instance._settlementToGive != null && __instance.CurrentSelectedClan != null)
        {
            MessageBroker.Instance.Publish(__instance, new GiftSettlementOwnership(__instance._settlementToGive, __instance.CurrentSelectedClan.Clan));
            GameThread.WaitWhilePumping(() => __instance._settlementToGive.OwnerClan == __instance.CurrentSelectedClan.Clan, DateTime.UtcNow.AddSeconds(5));
            __instance.ExecuteClose();
            __instance._onSettlementGranted();
        }
        return false;
    }
}

    

