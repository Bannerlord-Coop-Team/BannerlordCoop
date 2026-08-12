using Common;
using Common.Logging;
using GameInterface.Services.Inventory.TradeSkills.Interfaces;
using GameInterface.Services.MobileParties.Extensions;
using Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
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

        void UpdateEquipmentWithData(MobileParty mobileParty, Dictionary<CharacterObject, Equipment[]> characterEquipments, Hero initialHero);
    }

    internal class InventoryLogicInterface : IInventoryLogicInterface
    {
        static readonly ILogger logger = LogManager.GetLogger<InventoryLogicInterface>();

        private readonly ISessionTradePlayerDataInterface sessionTradePlayerDataInterface;
        private readonly IDefaultItemDiscardModelInterface defaultItemDiscardModelInterface;

        public InventoryLogicInterface(
            ISessionTradePlayerDataInterface sessionTradePlayerDataInterface,
            IDefaultItemDiscardModelInterface defaultItemDiscardModelInterface)
        {
            this.sessionTradePlayerDataInterface = sessionTradePlayerDataInterface;
            this.defaultItemDiscardModelInterface = defaultItemDiscardModelInterface;
        }

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
            GameThread.RunSafe(() =>
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
                // Transfers gold between player and other party (if party does not have enough gold, sends all gold)
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
            if (isDiscardDonating && ownerHero.PartyBelongedTo != null)
            {
                foreach (ItemRosterElement rosterElement in soldItems.Select(x => x.Item1))
                {
                    int xpBonusForDiscardingItems = defaultItemDiscardModelInterface.GetXpBonusForDiscardingItem(ownerHero.PartyBelongedTo, rosterElement.EquipmentElement.Item, rosterElement.Amount);
                    if ((float)xpBonusForDiscardingItems > 0f)
                    {
                        MobilePartyHelper.PartyAddSharedXp(ownerHero.PartyBelongedTo, (float)xpBonusForDiscardingItems);
                    }
                }
            }

            sessionTradePlayerDataInterface.UpdatePlayerInventory(ownerHero, boughtItems, soldItems, isTrading);
            if (currentSettlementComponent != null && isTrading)
            {
                // Sets the gold of the other party
                currentSettlementComponent.ChangeGold(totalAmount);
            }
            else if (((currentMobileParty != null) ? currentMobileParty.Party.LeaderHero : null) != null && isTrading)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, currentMobileParty.Party.LeaderHero, totalAmount, false);
                if (currentMobileParty.Party.LeaderHero.CompanionOf != null)
                {
                    currentMobileParty.AddTaxGold((int)(totalAmount * 0.1f));
                }
            }
            else if (partyBase != null && partyBase.LeaderHero == null && isTrading)
            {
                GiveGoldAction.ApplyForCharacterToParty(null, partyBase, totalAmount, false);
            }
        }

        public void UpdateRosterWithData(ItemRoster targetItemRoster, ItemRosterElement[] itemRosterElements)
        {
            if (itemRosterElements == null) return;

            var targetAmounts = SumByElement(itemRosterElements);
            var currentAmounts = SumByElement(targetItemRoster);

            // Remove items from roster that are no longer present in the target
            foreach (var currentElement in currentAmounts)
            {
                if (targetAmounts.ContainsKey(currentElement.Key)) continue;

                targetItemRoster.AddToCounts(currentElement.Value.EquipmentElement, -currentElement.Value.Amount);
            }

            // Apply deltas to change amounts in roster
            foreach (var targetElement in targetAmounts)
            {
                var currentElement = currentAmounts.TryGetValue(targetElement.Key, out var current) ? current : default;
                var delta = targetElement.Value.Amount - currentElement.Amount;

                if (delta == 0) continue;

                var element = currentElement.EquipmentElement.Item != null
                    ? currentElement.EquipmentElement
                    : targetElement.Value.EquipmentElement;

                targetItemRoster.AddToCounts(element, delta);
            }
        }

        private static Dictionary<string, ItemRosterElement> SumByElement(ItemRoster roster)
        {
            // Get non-zero elements from item roster
            var elements = new ItemRosterElement[roster.Count];
            for (int i = 0; i < roster.Count; i++)
            {
                elements[i] = roster.GetElementCopyAtIndex(i);
            }

            return SumByElement(elements);
        }

        private static Dictionary<string, ItemRosterElement> SumByElement(ItemRosterElement[] elements)
        {
            var totals = new Dictionary<string, ItemRosterElement>();

            foreach (var element in elements)
            {
                if (element.EquipmentElement.Item == null || element.Amount == 0) continue;

                var key = ElementKey(element.EquipmentElement);

                totals[key] = totals.TryGetValue(key, out var running)
                    ? new ItemRosterElement(running.EquipmentElement, running.Amount + element.Amount) // Increase existing with new amount
                    : element; // Doesn't exist yet, add to totals
            }

            return totals;
        }

        // Can't just use item id as there can be two or more items with the same id but a different modifier.
        private static string ElementKey(EquipmentElement equipmentElement)
        {
            return $"{equipmentElement.Item?.StringId}|{equipmentElement.ItemModifier?.StringId}";
        }

        public void UpdateEquipmentWithData(MobileParty mobileParty, Dictionary<CharacterObject, Equipment[]> characterEquipments, Hero initialHero)
        {
            GameThread.RunSafe(() =>
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
                UpdateMissionHeroVisuals(mobileParty);

                // When concluding an inventory screen managing a hero not in the main party, need to also update their party's visual
                initialHero.PartyBelongedTo.Party.SetVisualAsDirty();
            });
        }

        // Find and update all visuals of agents in a mission for managed heroes
        private void UpdateMissionHeroVisuals(MobileParty mobileParty)
        {
            // Return if the client isn't in a mission, nothing to update
            if (Mission.Current == null) return;

            foreach (Agent agent in Mission.Current.Agents)
            {
                CharacterObject characterObject = (CharacterObject)agent.Character;
                if (characterObject == null) continue;

                foreach (var troopRosterElement in mobileParty.MemberRoster.data)
                {
                    // May need to add handling for not updating disguised heroes later (e.g. !Campaign.Current.IsMainHeroDisguised)
                    if (troopRosterElement.Character == characterObject && characterObject.IsHero && characterObject.HeroObject.PartyBelongedTo.IsPlayerParty()) 
                    {
                        agent.UpdateSpawnEquipmentAndRefreshVisuals(Mission.Current.DoesMissionRequireCivilianEquipment ? characterObject.FirstCivilianEquipment : characterObject.FirstBattleEquipment);
                    }
                }
            }
        }
    }
}
