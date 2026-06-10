using Common;
using Common.Logging;
using Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.Core.Equipment;
using MathF = TaleWorlds.Library.MathF;

namespace GameInterface.Services.Inventory.Interfaces
{
    public interface IInventoryLogicInterface : IGameAbstraction
    {
        void ApplyDoneLogic(
            ItemRoster fromRoster,
            ItemRoster toRoster,
            bool isTrading,
            bool isDiscardDonating,
            Hero ownerHero,
            int totalAmount,
            int merchantGold,
            MobileParty currentMobileParty,
            SettlementComponent currentSettlementComponent,
            List<(ItemRosterElement, int)> boughtItems,
            List<(ItemRosterElement, int)> soldItems);

        void UpdateRosterWithData(ItemRoster targetItemRoster, ItemRosterElement[] itemRosterElements);

        void UpdateEquipmentWithData(MobileParty mobileParty, Dictionary<CharacterObject, Equipment[]> characterEquipments);
    }

    internal class InventoryLogicInterface : IInventoryLogicInterface
    {
        static readonly ILogger logger = LogManager.GetLogger<InventoryLogicInterface>();

        public void ApplyDoneLogic(
            ItemRoster fromRoster,
            ItemRoster toRoster,
            bool isTrading,
            bool isDiscardDonating,
            Hero ownerHero,
            int totalAmount,
            int merchantGold,
            MobileParty currentMobileParty,
            SettlementComponent currentSettlementComponent,
            List<ValueTuple<ItemRosterElement, int>> boughtItems,
            List<ValueTuple<ItemRosterElement, int>> soldItems)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                try
                {
                    ApplyDoneLogicInternal(
                        fromRoster,
                        toRoster,
                        isTrading, 
                        isDiscardDonating,
                        ownerHero, 
                        totalAmount, 
                        merchantGold,
                        currentMobileParty,
                        currentSettlementComponent,
                        boughtItems,
                        soldItems);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to {method}", nameof(ApplyDoneLogic));
                }
            });
        }

        private void ApplyDoneLogicInternal(
            ItemRoster fromRoster,
            ItemRoster toRoster,
            bool isTrading, 
            bool isDiscardDonating,
            Hero ownerHero,
            int totalAmount,
            int merchantGold,
            MobileParty currentMobileParty,
            SettlementComponent currentSettlementComponent,
            List<ValueTuple<ItemRosterElement, int>> boughtItems,
            List<ValueTuple<ItemRosterElement, int>> soldItems)
        {
            PartyBase partyBase = null;
            if (currentMobileParty != null)
            {
                partyBase = currentMobileParty.Party;
            }
            else if (currentSettlementComponent != null)
            {
                partyBase = currentSettlementComponent.Owner;
            }

            if (ownerHero.CharacterObject != null && ownerHero != null && isTrading)
            {
                // Trasnfers gold between player and other party (if party does not have enough gold, sends all gold)
                // Note: Total amount = transactional debt which is negative
                GiveGoldAction.ApplyBetweenCharacters(null, ownerHero, MathF.Min(-totalAmount, merchantGold), false);
                if (currentSettlementComponent != null && currentSettlementComponent.IsTown && ownerHero.CharacterObject.GetPerkValue(DefaultPerks.Trade.TrickleDown))
                {
                    int total = 0;

                    // Value is cost of item
                    // List<ValueTuple<ItemRosterElement, int>> boughtItems = __instance._transactionHistory.GetBoughtItems();

                    for (int i = 0; boughtItems != null && i < boughtItems.Count; i++)
                    {
                        ItemObject item = boughtItems[i].Item1.EquipmentElement.Item;
                        if (item != null && item.IsTradeGood)
                        {
                            total += boughtItems[i].Item2;
                        }
                    }
                    if (total >= 10000)
                    {
                        for (int i = 0; i < currentSettlementComponent.Settlement.Notables.Count; i++)
                        {
                            if (currentSettlementComponent.Settlement.Notables[i].IsMerchant)
                            {
                                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(currentSettlementComponent.Settlement.Notables[i], ownerHero, MathF.Floor(DefaultPerks.Trade.TrickleDown.PrimaryBonus), true);
                            }
                        }
                    }
                }
            }

            // Discarding items
            if (isDiscardDonating)
            {
                foreach (ItemRosterElement rosterElement in soldItems.Select(x => x.Item1))
                {
                    int xpBonusForDiscardingItems = Campaign.Current.Models.ItemDiscardModel.GetXpBonusForDiscardingItem(rosterElement.EquipmentElement.Item, rosterElement.Amount);
                    if ((float)xpBonusForDiscardingItems > 0f)
                    {
                        MobilePartyHelper.PartyAddSharedXp(ownerHero.PartyBelongedTo, (float)xpBonusForDiscardingItems);
                    }
                }
            }

            CampaignEventDispatcher.Instance.OnPlayerInventoryExchange(boughtItems, soldItems, isTrading);
            if (currentSettlementComponent != null && isTrading)
            {
                // Sets the gold of the other party
                currentSettlementComponent.Gold += totalAmount;
            }
            else if (((currentMobileParty != null) ? currentMobileParty.Party.LeaderHero : null) != null && isTrading)
            {
                // TODO
                GiveGoldAction.ApplyBetweenCharacters(null, currentMobileParty.Party.LeaderHero, totalAmount, false);
                if (currentMobileParty.Party.LeaderHero.CompanionOf != null)
                {
                    currentMobileParty.AddTaxGold((int)(totalAmount * 0.1f));
                }
            }
            else if (partyBase != null && partyBase.LeaderHero == null && isTrading)
            {
                // TODO
                GiveGoldAction.ApplyForCharacterToParty(null, partyBase, totalAmount, false);
            }
        }

        public void UpdateRosterWithData(ItemRoster targetItemRoster, ItemRosterElement[] itemRosterElements)
        {
            if (itemRosterElements == null) return;

            targetItemRoster.Clear();

            // Rebuild roster with new data
            targetItemRoster.Add(itemRosterElements);
        }

        public void UpdateEquipmentWithData(MobileParty mobileParty, Dictionary<CharacterObject, Equipment[]> characterEquipments)
        {
            foreach (KeyValuePair<CharacterObject, Equipment[]> characterEquipment in characterEquipments)
            {
                CharacterObject character = characterEquipment.Key;

                foreach (Equipment equipment in characterEquipment.Value)
                {
                    Equipment targetEquipment = null;
                    if (equipment._equipmentType == EquipmentType.Battle)
                    {
                        targetEquipment = character.FirstBattleEquipment;
                    }
                    else if (equipment._equipmentType == EquipmentType.Civilian)
                    {
                        targetEquipment = character.FirstCivilianEquipment;
                    }
                    else if (equipment._equipmentType == EquipmentType.Stealth)
                    {
                        targetEquipment = character.FirstStealthEquipment;
                    }

                    if (targetEquipment != null)
                    {
                        for (int i = 0; i < EquipmentSlotLength; i++)
                        {
                            targetEquipment._itemSlots[i] = equipment._itemSlots[i];
                        }
                    }
                }
            }

            mobileParty.Party.SetVisualAsDirty();
        }
    }
}
