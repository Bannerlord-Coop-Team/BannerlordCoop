using Common;
using Common.Logging;
using Common.LogicStates;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Inventory.Messages;
using HarmonyLib;
using Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.View;

namespace GameInterface.Services.Inventory.Patches;

[HarmonyPatch(typeof(InventoryLogic))]
internal class InventoryLogicPatches
{
    private static readonly ILogger logger = LogManager.GetLogger<InventoryLogicPatches>();

    [HarmonyPatch(nameof(InventoryLogic.DoneLogic))]
    [HarmonyPrefix]
    static bool DoneLogicPrefix(ref InventoryLogic __instance, ref bool __result)
    {
        if (__instance.IsPreviewingItem)
        {
            __result = false;
            return false;
        }

        // Don't want to run done logic here for trading as trading transactions managed instantly
        if (__instance._inventoryMode == InventoryScreenHelper.InventoryMode.Trade) return true;

        /*
        SettlementComponent currentSettlementComponent = __instance.CurrentSettlementComponent;
        MobileParty currentMobileParty = __instance.CurrentMobileParty;
        PartyBase partyBase = null;
        if (currentMobileParty != null)
        {
            partyBase = currentMobileParty.Party;
        }
        else if (currentSettlementComponent != null)
        {
            partyBase = currentSettlementComponent.Owner;
        }
        */

        /*
        if (__instance.InventoryListener != null && __instance.IsTrading && __instance.OwnerCharacter.HeroObject.Gold - __instance.TotalAmount < 0)
        {
            MBInformationManager.AddQuickInformation(GameTexts.FindText("str_warning_you_dont_have_enough_money", null), 0, null, null, "");
            __result = false;
            return false;
        }
        */

        if (__instance._playerAcceptsTraderOffer)
        {
            __instance._playerAcceptsTraderOffer = false;
            if (__instance.InventoryListener != null)
            {
                int gold = __instance.InventoryListener.GetGold();
                __instance.TransactionDebt = -gold;
            }
        }

        __instance._partyInitialEquipment = new InventoryLogic.PartyEquipment(__instance.OwnerParty);
        if (__instance.IsOtherPartyFromPlayerClan && __instance.LeftMemberRoster != null)
        {
            __instance._otherPartyInitialEquipment = new InventoryLogic.PartyEquipment(__instance.OtherParty.MobileParty);
        }

        var message = new TradeAttempted(
            __instance._rosters[0],
            __instance._rosters[1],
            __instance.IsTrading,
            __instance.CanGainXpFromDiscarding,
            __instance.OwnerParty.LeaderHero,
            __instance.TotalAmount,
            __instance.InventoryListener.GetGold(),
            __instance.OwnerParty,
            __instance.CurrentMobileParty,
            __instance.CurrentSettlementComponent,
            __instance.GetBoughtItems(),
            __instance.GetSoldItems()
        );

        MessageBroker.Instance.Publish(__instance, message);

        // Reset rosters so they are set on the server side
        //using (new AllowedThread())
        //{
        //    __instance.Reset(true);
        //}

        __result = true;
        return false;
    }

    [HarmonyPatch(nameof(InventoryLogic.ResetLogic))]
    [HarmonyPrefix]
    public static bool ResetLogicPrefix(ref InventoryLogic __instance, bool fromCancel)
    {
        // Don't want reset to run for trading as trading transactions managed instantly
        if (__instance._inventoryMode == InventoryScreenHelper.InventoryMode.Trade) return false;

        AllowedThread.AllowThisThread();
        for (int i = 0; i < 2; i++)
        {
            __instance._rosters[i].Clear();
            __instance._rosters[i].Add(__instance._rostersBackup[i]);
        }
        AllowedThread.RevokeThisThread();

        var message = new ResetRosters(
            __instance._rosters[0],
            __instance._rostersBackup[0],
            __instance._rosters[1],
            __instance._rostersBackup[1],
            __instance, fromCancel
        );
        MessageBroker.Instance.Publish(__instance, message);

        __instance.TransactionDebt = 0;
        __instance._transactionHistory.Clear();
        __instance.InitializeXpGainFromDonations();
        __instance._partyInitialEquipment.ResetEquipment();
        InventoryLogic.PartyEquipment otherPartyInitialEquipment = __instance._otherPartyInitialEquipment;
        if (otherPartyInitialEquipment != null)
        {
            otherPartyInitialEquipment.ResetEquipment();
        }

        FieldInfo field = typeof(InventoryLogic).GetField("AfterReset", BindingFlags.Instance | BindingFlags.NonPublic);
        InventoryLogic.AfterResetDelegate del = (InventoryLogic.AfterResetDelegate)field.GetValue(__instance);

        InventoryLogic.AfterResetDelegate afterReset = del;
        if (afterReset != null)
        {
            afterReset(__instance, fromCancel);
        }

        List<TransferCommandResult> resultList = new List<TransferCommandResult>();
        if (!fromCancel)
        {
            __instance.OnAfterTransfer(resultList);
        }

        return false;
    }

    [HarmonyPatch(nameof(InventoryLogic.TransferItem))]
    [HarmonyPrefix]
    public static bool TransferItemPrefix(ref InventoryLogic __instance, ref List<TransferCommandResult> __result, ref TransferCommand transferCommand)
    {
        // Ensure the player has enough gold if trading to transfer the item
        if (__instance._inventoryMode != InventoryScreenHelper.InventoryMode.Trade) return true;

        bool isSell = __instance.IsSell(transferCommand.FromSide, transferCommand.ToSide);
        bool isBuy = __instance.IsBuy(transferCommand.FromSide, transferCommand.ToSide);

        int transactionDebt = 0;
        int itemPrice = __instance.GetItemPrice(transferCommand.ElementToTransfer.EquipmentElement, isBuy);
        if (isSell)
        {
            transactionDebt -= itemPrice;
        }
        else if (isBuy)
        {
            transactionDebt += itemPrice;
        }

        if (__instance.InventoryListener != null && __instance.OwnerCharacter.HeroObject.Gold - transactionDebt < 0)
        {
            __result = new List<TransferCommandResult>();

            MBInformationManager.AddQuickInformation(GameTexts.FindText("str_warning_you_dont_have_enough_money", null), 0, null, null, "");
            return false;
        }

        return true;
    }

    [HarmonyPatch(nameof(InventoryLogic.TransferItem))]
    [HarmonyPostfix]
    public static void TransferItemPostfix(ref InventoryLogic __instance, ref TransferCommand transferCommand)
    {
        if (__instance._inventoryMode == InventoryScreenHelper.InventoryMode.Trade && __instance.TransactionDebt == 0) return;

        EquipmentElement equipmentElement = transferCommand.ElementToTransfer.EquipmentElement;
        bool shouldManageOtherInventory = ShouldManageOtherInventory(ref __instance);
        int amount = transferCommand.Amount;

        ItemRoster fromItemRoster = null;
        ItemRoster toItemRoster = null;

        // Both of these can be true, send messages for each
        AllowedThread.AllowThisThread();
        if (transferCommand.FromSide == InventoryLogic.InventorySide.PlayerInventory || (transferCommand.FromSide == InventoryLogic.InventorySide.OtherInventory && shouldManageOtherInventory))
        {
            // Reverse operation needs to be performed in main body to avoid crashing, add back so the server can manage from here
            fromItemRoster = __instance._rosters[(int)transferCommand.FromSide];
            GameLoopRunner.RunOnMainThread(() =>
            {
                fromItemRoster.AddToCounts(equipmentElement, amount);
            });
        }

        if (transferCommand.ToSide == InventoryLogic.InventorySide.PlayerInventory || (transferCommand.ToSide == InventoryLogic.InventorySide.OtherInventory && shouldManageOtherInventory))
        {
            // Reverse operation needs to be performed in main body to avoid crashing, remove so the server can manage from here
            toItemRoster = __instance._rosters[(int)transferCommand.ToSide];
            GameLoopRunner.RunOnMainThread(() =>
            {
                toItemRoster.AddToCounts(equipmentElement, -amount);
            });
        }
        AllowedThread.RevokeThisThread();

        var transferMessage = new TransferAttempted(
            fromItemRoster,
            toItemRoster,
            equipmentElement,
            transferCommand.Amount
        );

        MessageBroker.Instance.Publish(__instance, transferMessage);

        // Make trade instant by implementing DoneLogic here
        if (__instance._inventoryMode == InventoryScreenHelper.InventoryMode.Trade)
        {
            var tradeMessage = new TradeAttempted(
            __instance._rosters[0],
            __instance._rosters[1],
            __instance.IsTrading,
            __instance.CanGainXpFromDiscarding,
            __instance.OwnerParty.LeaderHero,
            __instance.TotalAmount,
            __instance.InventoryListener.GetGold(),
            __instance.OwnerParty,
            __instance.CurrentMobileParty,
            __instance.CurrentSettlementComponent,
            __instance.GetBoughtItems(),
            __instance.GetSoldItems()
            );

            MessageBroker.Instance.Publish(__instance, tradeMessage);

            // Play coin change sound cues
            if (__instance.TransactionDebt > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("", "event:/ui/notification/coins_negative"));
            }
            else if (__instance.TransactionDebt < 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("", "event:/ui/notification/coins_positive"));
            }

            // Clear transaction history for next transfer
            __instance.TransactionDebt = 0;
            __instance._transactionHistory.Clear();
        }
    }

    // Some inventory modes such as the default don't need the other item roster to be managed on the server
    // For example, the default inventory mode's other roster is for discarding items and is always cleared when the SPInventoryVM is closed
    private static bool ShouldManageOtherInventory(ref InventoryLogic logic)
    {
        // Expand as needed for other inventory modes
        return logic._inventoryMode != InventoryScreenHelper.InventoryMode.Default 
            && logic._inventoryMode != InventoryScreenHelper.InventoryMode.Loot;
    }

    [HarmonyPatch(nameof(InventoryLogic.SlaughterItem))]
    [HarmonyPostfix]
    public static void SlaughterItemPostfix(ref InventoryLogic __instance, ItemRosterElement itemRosterElement)
    {
        EquipmentElement equipmentElement = itemRosterElement.EquipmentElement;
        int meatCount = equipmentElement.Item.HorseComponent.MeatCount;
        int hideCount = equipmentElement.Item.HorseComponent.HideCount;

        // Undo AddToCounts operations so server can manage
        ItemRoster itemRoster = __instance._rosters[1];
        GameLoopRunner.RunOnMainThread(() =>
        {
            itemRoster.AddToCounts(DefaultItems.Meat, -meatCount);
            itemRoster.AddToCounts(itemRosterElement.EquipmentElement, 1);
            itemRoster.AddToCounts(DefaultItems.Hides, -hideCount);
        });

        // Send data to server
        var message = new ItemSlaughtered(itemRoster, equipmentElement, meatCount, hideCount);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(InventoryLogic.DonateItem))]
    [HarmonyPrefix]
    public static bool DonateItemPrefix(ref InventoryLogic __instance, ItemRosterElement itemRosterElement)
    {
        List<TransferCommandResult> list = new List<TransferCommandResult>();
        int tier = (int)itemRosterElement.EquipmentElement.Item.Tier;
        int num = 100 * (tier + 1);
        InventoryLogic.InventorySide inventorySide = InventoryLogic.InventorySide.PlayerInventory;
        int index = __instance._rosters[(int)inventorySide].AddToCounts(itemRosterElement.EquipmentElement, -1);
        ItemRosterElement elementCopyAtIndex = __instance._rosters[(int)inventorySide].GetElementCopyAtIndex(index);
        list.Add(new TransferCommandResult(InventoryLogic.InventorySide.PlayerInventory, elementCopyAtIndex, -1, elementCopyAtIndex.Amount, EquipmentIndex.None, null));

        TroopRosterElement randomElementWithPredicate = PartyBase.MainParty.MemberRoster.GetTroopRoster().GetRandomElementWithPredicate((TroopRosterElement m) => !m.Character.IsHero && m.Character.UpgradeTargets.Length != 0);
        if (num > 0)
        {
            if (randomElementWithPredicate.Character != null)
            {
                // Manage on server instead, still need this block to display notification
                //PartyBase.MainParty.MemberRoster.AddXpToTroop(randomElementWithPredicate.Character, num);
                TextObject textObject = new TextObject("{=Kwja0a4s}Added {XPAMOUNT} amount of xp to {TROOPNAME}", null);
                textObject.SetTextVariable("XPAMOUNT", num);
                textObject.SetTextVariable("TROOPNAME", randomElementWithPredicate.Character.Name.ToString());
                MBInformationManager.AddQuickInformation(textObject, 0, null, null, "");
            }
        }
        __instance.SetCurrentStateAsInitial();
        __instance.OnAfterTransfer(list);

        // Undo AddToCounts action to let server manage from here. Previously run to get the index and also to update the client's VM correctly
        __instance._rosters[(int)inventorySide].AddToCounts(itemRosterElement.EquipmentElement, 1);

        var message = new ItemDonated(
            __instance._rosters[(int)inventorySide],
            itemRosterElement.EquipmentElement,
            PartyBase.MainParty, randomElementWithPredicate, num);

        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}
