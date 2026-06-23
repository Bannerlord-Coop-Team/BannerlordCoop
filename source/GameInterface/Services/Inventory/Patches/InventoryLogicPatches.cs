using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Patches;

[HarmonyPatch(typeof(InventoryLogic))]
internal class InventoryLogicPatches
{
    private static readonly ILogger logger = LogManager.GetLogger<InventoryLogicPatches>();

    [HarmonyPatch(nameof(InventoryLogic.DoneLogic))]
    [HarmonyPrefix]
    static bool DoneLogicPrefix(InventoryLogic __instance, ref bool __result)
    {
        //if (PlayerPartyTradeContext.IsActive)
        //{
        //    PlayerPartyTradeContext.PublishAccept(true);
        //    __result = true;
        //    return false;
        //}

        if (__instance.IsPreviewingItem)
        {
            __result = false;
            return false;
        }

        if (__instance.InventoryListener != null && __instance.IsTrading && __instance.OwnerCharacter.HeroObject.Gold - __instance.TotalAmount < 0)
        {
            MBInformationManager.AddQuickInformation(GameTexts.FindText("str_warning_you_dont_have_enough_money", null), 0, null, null, "");
            __result = false;
            return false;
        }

        if (__instance._playerAcceptsTraderOffer)
        {
            __instance._playerAcceptsTraderOffer = false;
            if (__instance.InventoryListener != null)
            {
                int gold = __instance.InventoryListener.GetGold();
                __instance.TransactionDebt = -gold;
            }
        }

        // Send rosters and equipment slots to server to manage
        var message = new TradeAttempted(
            __instance._rosters[0],
            __instance._rosters[1],
            __instance.IsTrading,
            __instance.CanGainXpFromDiscarding,
            __instance._inventoryMode,
            __instance.OwnerParty.LeaderHero,
            __instance.InitialEquipmentCharacter,
            __instance.TotalAmount,
            __instance.InventoryListener.GetGold(),
            __instance.OwnerParty,
            __instance.CurrentMobileParty,
            __instance.CurrentSettlementComponent,
            __instance.GetBoughtItems(),
            __instance.GetSoldItems(),
            PartyBase.MainParty.MemberRoster
        );

        MessageBroker.Instance.Publish(__instance, message);

        // Reset rosters so they are set on the server side
        using (new AllowedThread())
        {
            __instance.Reset(true);
        }

        __result = true;
        return false;
    }

    //[HarmonyPatch(nameof(InventoryLogic.TransferItem))]
    //[HarmonyPrefix]
    //static bool TransferItemPrefix(ref TransferCommand transferCommand, ref List<TransferCommandResult> __result)
    //{
    //    if (PlayerPartyTradeContext.CanTransfer(transferCommand)) return true;

    //    __result = new List<TransferCommandResult>();
    //    return false;
    //}

    //[HarmonyPatch(nameof(InventoryLogic.TransferItem))]
    //[HarmonyPostfix]
    //static void TransferItemPostfix(InventoryLogic __instance)
    //{
    //    PlayerPartyTradeContext.PublishOfferChanged(__instance);
    //}
}
