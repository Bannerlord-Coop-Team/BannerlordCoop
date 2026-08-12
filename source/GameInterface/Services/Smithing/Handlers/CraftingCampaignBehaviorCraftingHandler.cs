using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Serialization;
using GameInterface.Services.ItemObjects.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using GameInterface.Services.Smithing.Patches;
using GameInterface.Services.Transactions;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Services.Smithing.Handlers
{
    internal class CraftingCampaignBehaviorCraftingHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehaviorCraftingHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly IBinaryPackageFactory binaryPackageFactory;
        private readonly IItemObjectInterface itemObjectInterface;
        private readonly ISessionCraftingPlayerDataInterface sessionCraftingPlayerDataInterface;
        private readonly ISendCoalescer sendCoalescer;
        private readonly IPlayerManager playerManager;
        private readonly ICraftingCampaignBehaviorInterface craftingCampaignBehaviorInterface;

        public CraftingCampaignBehaviorCraftingHandler(
            IMessageBroker messageBroker,
            IObjectManager objectManager,
            INetwork network,
            IBinaryPackageFactory binaryPackageFactory,
            IItemObjectInterface itemObjectInterface,
            ISessionCraftingPlayerDataInterface sessionCraftingPlayerDataInterface,
            IPlayerManager playerManager,
            ICraftingCampaignBehaviorInterface craftingCampaignBehaviorInterface,
            ISendCoalescer sendCoalescer = null)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            this.binaryPackageFactory = binaryPackageFactory;
            this.itemObjectInterface = itemObjectInterface;
            this.sessionCraftingPlayerDataInterface = sessionCraftingPlayerDataInterface;
            this.playerManager = playerManager;
            this.craftingCampaignBehaviorInterface = craftingCampaignBehaviorInterface;
            this.sendCoalescer = sendCoalescer;

            messageBroker.Subscribe<DoSmelting>(Handle);
            messageBroker.Subscribe<NetworkDoSmelting>(Handle);
            messageBroker.Subscribe<DoRefinement>(Handle);
            messageBroker.Subscribe<NetworkDoRefinement>(Handle);
            messageBroker.Subscribe<CreatedCraftedWeaponInternal>(Handle);
            messageBroker.Subscribe<NetworkCreateCraftedWeaponInternalServer>(Handle);
            messageBroker.Subscribe<NetworkCreateCraftedWeaponInternalClients>(Handle);
            messageBroker.Subscribe<NetworkAddCraftedItemToRoster>(Handle);

            messageBroker.Subscribe<NetworkSetHeroCraftingStamina>(Handle);

            messageBroker.Subscribe<AddSkillXpFromCrafting>(Handle);
            messageBroker.Subscribe<NetworkAddSkillXpFromCrafting>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<DoSmelting>(Handle);
            messageBroker.Unsubscribe<NetworkDoSmelting>(Handle);
            messageBroker.Unsubscribe<DoRefinement>(Handle);
            messageBroker.Unsubscribe<NetworkDoRefinement>(Handle);
            messageBroker.Unsubscribe<CreatedCraftedWeaponInternal>(Handle);
            messageBroker.Unsubscribe<NetworkCreateCraftedWeaponInternalServer>(Handle);
            messageBroker.Unsubscribe<NetworkCreateCraftedWeaponInternalClients>(Handle);
            messageBroker.Unsubscribe<NetworkAddCraftedItemToRoster>(Handle);

            messageBroker.Unsubscribe<NetworkSetHeroCraftingStamina>(Handle);

            messageBroker.Unsubscribe<AddSkillXpFromCrafting>(Handle);
            messageBroker.Unsubscribe<NetworkAddSkillXpFromCrafting>(Handle);
        }

        private void Handle(MessagePayload<DoSmelting> obj)
        {
            SendSmeltingDone(obj.What);
        }

        private void Handle(MessagePayload<NetworkDoSmelting> obj)
        {
            DoSmelting(obj.What, obj.Who as NetPeer);
        }

        private void Handle(MessagePayload<DoRefinement> obj)
        {
            SendRefinementDone(obj.What);
        }

        private void Handle(MessagePayload<NetworkDoRefinement> obj)
        {
            DoRefinement(obj.What, obj.Who as NetPeer);
        }

        private void Handle(MessagePayload<CreatedCraftedWeaponInternal> obj)
        {
            SendInternallyCreatedWeapon(obj.What);
        }

        private void Handle(MessagePayload<NetworkCreateCraftedWeaponInternalServer> obj)
        {
            CreateCraftedWeaponInternalServer(
                obj.What, obj.Who as NetPeer);
        }

        private void Handle(MessagePayload<NetworkCreateCraftedWeaponInternalClients> obj)
        {
            if (ModInformation.IsServer) return;
            CreateCraftedWeaponInternalClients(obj.What);
        }

        private void Handle(MessagePayload<NetworkAddCraftedItemToRoster> obj)
        {
            if (ModInformation.IsServer) return;
            AddCraftedItemToRoster(obj.What);
        }

        private void Handle(MessagePayload<NetworkSetHeroCraftingStamina> obj)
        {
            if (ModInformation.IsServer) return;
            SetHeroCraftingStaminaClients(obj.What);
        }

        private void Handle(MessagePayload<AddSkillXpFromCrafting> obj)
        {
            GameThread.RunSafe(() =>
            {
                if (!objectManager.TryGetIdWithLogging(obj.What.CraftingHero, out var craftingHeroId)) return;

                network.SendAll(new NetworkAddSkillXpFromCrafting(craftingHeroId, obj.What.Xp));
            });
        }

        private void Handle(MessagePayload<NetworkAddSkillXpFromCrafting> obj)
        {
            NetPeer peer = obj.Who as NetPeer;
            GameThread.RunSafe(() => ServerTransactionOutcome.Execute(
                peer, ServerTransactionOutcome.CraftXp, () =>
            {
                if (!ServerTransactionOutcome.TryResolveOwnedCraftingHero(
                        peer,
                        playerManager,
                        objectManager,
                        obj.What.CraftingHeroId,
                        out _,
                        out _,
                        out _,
                        out string authenticationReason) ||
                    !ServerTransactionOutcome.TryConsumeCraftXp(
                        peer, obj.What.CraftingHeroId) ||
                    float.IsNaN(obj.What.Xp) ||
                    float.IsInfinity(obj.What.Xp) || obj.What.Xp < 0f)
                {
                    ServerTransactionOutcome.Reject(
                        peer, ServerTransactionOutcome.CraftXp,
                        string.IsNullOrEmpty(authenticationReason)
                            ? "Crafting experience was not linked to an accepted craft."
                            : authenticationReason);
                    return;
                }

                // XP is calculated and applied with the accepted craft on the
                // server. This paired legacy packet is confirmation only.
                ServerTransactionOutcome.Accept(
                    peer, ServerTransactionOutcome.CraftXp);
            }));
        }

        private void SendSmeltingDone(DoSmelting obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.CraftingHero, out var craftingHeroId)) return;
            network.SendAll(new NetworkDoSmelting(
                craftingHeroId, obj.EquipmentElement));
        }

        private void DoSmelting(NetworkDoSmelting obj, NetPeer peer)
        {
            GameThread.RunSafe(() => ServerTransactionOutcome.Execute(
                peer, ServerTransactionOutcome.Smelt, () =>
            {
                if (!ServerTransactionOutcome.TryResolveOwnedCraftingHero(
                        peer,
                        playerManager,
                        objectManager,
                        obj.CraftingHeroId,
                        out var player,
                        out Hero authorizedHero,
                        out MobileParty playerParty,
                        out string authenticationReason))
                {
                    RejectSmithing(
                        peer,
                        ServerTransactionOutcome.Smelt,
                        authenticationReason);
                    return;
                }
                if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out CraftingCampaignBehavior craftingCampaignBehavior) ||
                    authorizedHero?.PartyBelongedTo == null ||
                    !IsSmithyAvailable(playerParty))
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Smelt,
                        "The smithing state is no longer available.");
                    return;
                }
                Hero craftingHero = authorizedHero;

                EquipmentElement equipmentElement = obj.EquipmentElement;
                ItemObject item = equipmentElement.Item;
                if (item == null)
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Smelt,
                        "The selected item is no longer available.");
                    return;
                }
                if (equipmentElement.IsQuestItem)
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Smelt,
                        "Quest items cannot be smelted.");
                    return;
                }

                // Replace original TaleWorlds implementation
                ItemRoster itemRoster = craftingHero.PartyBelongedTo.ItemRoster;
                int[] smeltingOutputForItem = Campaign.Current.Models.SmithingModel.GetSmeltingOutputForItem(item);

                int smeltItemIndex = itemRoster.FindIndexOfElement(equipmentElement);
                if (smeltItemIndex < 0 || itemRoster.GetElementCopyAtIndex(smeltItemIndex).Amount < 1)
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Smelt,
                        "That item is no longer available to smelt.");
                    return;
                }
                int energyCostForSmelting = Campaign.Current.Models.SmithingModel.GetEnergyCostForSmelting(item, craftingHero);
                int currentStamina = craftingCampaignBehavior.GetHeroCraftingStamina(craftingHero);
                if (energyCostForSmelting < 0 || currentStamina < energyCostForSmelting)
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Smelt,
                        "The selected hero does not have enough smithing stamina.");
                    return;
                }
                ItemRosterElement[] itemRosterBefore = itemRoster.ToArray();
                int newHeroCraftingStamina =
                    currentStamina - energyCostForSmelting;
                try
                {
                    itemRoster.AddToCounts(equipmentElement, -1);
                    for (int i = 8; i >= 0; i--)
                    {
                        if (smeltingOutputForItem[i] != 0)
                            itemRoster.AddToCounts(
                                Campaign.Current.Models.SmithingModel
                                    .GetCraftingMaterialItem(
                                        (CraftingMaterials)i),
                                smeltingOutputForItem[i]);
                    }
                    craftingCampaignBehavior.SetHeroCraftingStamina(
                        craftingHero, newHeroCraftingStamina);
                }
                catch (Exception exception)
                {
                    RestoreSmithingCore(
                        itemRoster,
                        itemRosterBefore,
                        craftingCampaignBehavior,
                        craftingHero,
                        currentStamina,
                        exception,
                        "smelting");
                    RejectSmithing(
                        peer,
                        ServerTransactionOutcome.Smelt,
                        "Smelting could not be committed safely.");
                    return;
                }

                TryPostCommit(() => craftingHero.AddSkillXp(
                    DefaultSkills.Crafting,
                    Campaign.Current.Models.SmithingModel
                        .GetSkillXpForSmelting(equipmentElement.Item)),
                    "smelting skill XP");
                TryPostCommit(() => network.SendAll(
                    new NetworkSetHeroCraftingStamina(
                        obj.CraftingHeroId,
                        newHeroCraftingStamina)), "smelting stamina");
                TryPostCommit(() => CampaignEventDispatcher.Instance
                    .OnEquipmentSmeltedByHero(
                        craftingHero, equipmentElement),
                    "equipment-smelted event");

                if (item.WeaponDesign?.Template != null &&
                    objectManager.TryGetId(
                        item.WeaponDesign.Template, out string templateId))
                {
                    TryPostCommit(() =>
                    {
                        int researchGain = Campaign.Current.Models.SmithingModel
                            .GetPartResearchGainForSmeltingItem(
                                item, craftingHero);
                        CraftingCampaignBehaviorResearchPointHandler
                            .AllowAuthoritativeResearch(
                                peer,
                                player.HeroId,
                                templateId,
                                researchGain);
                    }, "smelting research permit");
                }

                TryPostCommit(() =>
                {
                    objectManager.TryGetId(itemRoster, out var rosterId);
                    var compactId = Compact(rosterId, typeof(ItemRoster));
                    sendCoalescer?.FlushInstance(compactId, network);
                }, "smelting roster flush");
                TryPostCommit(() => network.SendAll(
                    new NetworkRefreshSmelting()), "smelting refresh");
                ServerTransactionOutcome.Accept(
                    peer, ServerTransactionOutcome.Smelt);
            }));
        }

        private void SendRefinementDone(DoRefinement obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.CraftingHero, out var craftingHeroId)) return;

            // Need to reconstruct formula at the other end
            Crafting.RefiningFormula formula = obj.RefiningFormula;

            // Send to server from client
            NetworkDoRefinement message = new(
                craftingHeroId,
                formula.Input1,
                formula.Input1Count,
                formula.Input2,
                formula.Input2Count,
                formula.Output,
                formula.OutputCount,
                formula.Output2,
                formula.Output2Count
            );
            network.SendAll(message);
        }

        private void DoRefinement(NetworkDoRefinement obj, NetPeer peer)
        {
            GameThread.RunSafe(() => ServerTransactionOutcome.Execute(
                peer, ServerTransactionOutcome.Refine, () =>
            {
                if (!ServerTransactionOutcome.TryResolveOwnedCraftingHero(
                        peer,
                        playerManager,
                        objectManager,
                        obj.CraftingHeroId,
                        out _,
                        out Hero authorizedHero,
                        out MobileParty playerParty,
                        out string authenticationReason))
                {
                    if (string.IsNullOrEmpty(authenticationReason))
                        authenticationReason =
                            "The crafting request did not belong to this player.";
                    RejectSmithing(
                        peer,
                        ServerTransactionOutcome.Refine,
                        authenticationReason);
                    return;
                }
                // Get objects from objectManager
                if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out CraftingCampaignBehavior craftingCampaignBehavior) ||
                    authorizedHero?.PartyBelongedTo == null ||
                    !IsSmithyAvailable(playerParty))
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Refine,
                        "The smithing state is no longer available.");
                    return;
                }
                Hero craftingHero = authorizedHero;

                // Rebuild formula on server
                var formula = new Crafting.RefiningFormula(
                    obj.Input1, obj.Input1Count,
                    obj.Input2, obj.Input2Count,
                    obj.Output1, obj.Output1Count,
                    obj.Output2, obj.Output2Count);

                if (!Campaign.Current.Models.SmithingModel
                        .GetRefiningFormulas(craftingHero)
                        .Any(candidate => RefiningFormulasMatch(
                            candidate, formula)))
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Refine,
                        "That refining formula is not available to this hero.");
                    return;
                }

                // Replace original TaleWorlds implementation
                ItemRoster itemRoster = craftingHero.PartyBelongedTo.ItemRoster;
                var requiredMaterials = new Dictionary<CraftingMaterials, int>();
                AddRequiredMaterial(
                    requiredMaterials, formula.Input1, formula.Input1Count);
                AddRequiredMaterial(
                    requiredMaterials, formula.Input2, formula.Input2Count);
                foreach (KeyValuePair<CraftingMaterials, int> required in requiredMaterials)
                {
                    ItemObject material = Campaign.Current.Models.SmithingModel
                        .GetCraftingMaterialItem(required.Key);
                    if (required.Value < 0 ||
                        itemRoster.GetItemNumber(material) < required.Value)
                    {
                        RejectSmithing(peer, ServerTransactionOutcome.Refine,
                            "The required smithing materials are no longer available.");
                        return;
                    }
                }
                int energyCostForRefining = Campaign.Current.Models.SmithingModel
                    .GetEnergyCostForRefining(ref formula, craftingHero);
                int currentStamina = craftingCampaignBehavior
                    .GetHeroCraftingStamina(craftingHero);
                if (energyCostForRefining < 0 ||
                    currentStamina < energyCostForRefining)
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Refine,
                        "The selected hero does not have enough smithing stamina.");
                    return;
                }
                ItemRosterElement[] itemRosterBefore = itemRoster.ToArray();
                int newHeroCraftingStamina =
                    currentStamina - energyCostForRefining;
                try
                {
                    if (formula.Input1Count > 0)
                        itemRoster.AddToCounts(
                            Campaign.Current.Models.SmithingModel
                                .GetCraftingMaterialItem(formula.Input1),
                            -formula.Input1Count);
                    if (formula.Input2Count > 0)
                        itemRoster.AddToCounts(
                            Campaign.Current.Models.SmithingModel
                                .GetCraftingMaterialItem(formula.Input2),
                            -formula.Input2Count);
                    if (formula.OutputCount > 0)
                        itemRoster.AddToCounts(
                            Campaign.Current.Models.SmithingModel
                                .GetCraftingMaterialItem(formula.Output),
                            formula.OutputCount);
                    if (formula.Output2Count > 0)
                        itemRoster.AddToCounts(
                            Campaign.Current.Models.SmithingModel
                                .GetCraftingMaterialItem(formula.Output2),
                            formula.Output2Count);
                    craftingCampaignBehavior.SetHeroCraftingStamina(
                        craftingHero, newHeroCraftingStamina);
                }
                catch (Exception exception)
                {
                    RestoreSmithingCore(
                        itemRoster,
                        itemRosterBefore,
                        craftingCampaignBehavior,
                        craftingHero,
                        currentStamina,
                        exception,
                        "refining");
                    RejectSmithing(
                        peer,
                        ServerTransactionOutcome.Refine,
                        "Refining could not be committed safely.");
                    return;
                }

                TryPostCommit(() =>
                {
                    Crafting.RefiningFormula xpFormula = formula;
                    craftingHero.AddSkillXp(
                        DefaultSkills.Crafting,
                        Campaign.Current.Models.SmithingModel
                            .GetSkillXpForRefining(ref xpFormula));
                }, "refining skill XP");
                TryPostCommit(() => network.SendAll(
                    new NetworkSetHeroCraftingStamina(
                        obj.CraftingHeroId,
                        newHeroCraftingStamina)), "refining stamina");
                TryPostCommit(() => CampaignEventDispatcher.Instance
                    .OnItemsRefined(craftingHero, formula),
                    "items-refined event");
                TryPostCommit(() =>
                {
                    objectManager.TryGetId(itemRoster, out var rosterId);
                    var compactId = Compact(rosterId, typeof(ItemRoster));
                    sendCoalescer?.FlushInstance(compactId, network);
                }, "refining roster flush");
                TryPostCommit(() => network.SendAll(
                    new NetworkRefreshRefinement(obj.CraftingHeroId)),
                    "refining refresh");
                ServerTransactionOutcome.Accept(
                    peer, ServerTransactionOutcome.Refine);
            }));
        }

        private void SendInternallyCreatedWeapon(CreatedCraftedWeaponInternal obj)
        {
            if (!objectManager.TryGetIdWithLogging(obj.CraftingHero, out var craftingHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.WeaponDesign.Template, out var craftingTemplateId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.PlayerHero, out var playerHeroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.CurrentSettlement, out var currentSettlementId)) return;

            string itemModifierGroupId = null;
            if (obj.CraftingLogic.CurrentItemModifierGroup != null && !objectManager.TryGetIdWithLogging(obj.CraftingLogic.CurrentItemModifierGroup, out itemModifierGroupId)) return;

            string cultureId = null;
            if (obj.CultureObject != null && !objectManager.TryGetIdWithLogging(obj.CultureObject, out cultureId)) return;

            var weaponDesignElementCraftingPieceIds = new List<string>();
            var weaponDesignElementScalePercentages = new List<int>();
            foreach (var weaponDesignElement in obj.WeaponDesign._usedPieces)
            {
                if (!weaponDesignElement._craftingPiece.IsValid) // Skip invalid crafting pieces, e.g. Axe doesn't have a guard
                {
                    weaponDesignElementCraftingPieceIds.Add("");
                    weaponDesignElementScalePercentages.Add(-1);
                    continue;
                }

                if (!objectManager.TryGetIdWithLogging(weaponDesignElement._craftingPiece, out var currentCraftingPieceId)) return;
                weaponDesignElementCraftingPieceIds.Add(currentCraftingPieceId);
                weaponDesignElementScalePercentages.Add(weaponDesignElement._scalePercentage);
            }

            var weaponModifierId = "";
            if (obj.WeaponModifier != null && !objectManager.TryGetIdWithLogging(obj.WeaponModifier, out weaponModifierId)) return;

            string craftingOrderId = null;
            if (obj.CraftingOrder != null && !objectManager.TryGetIdWithLogging(obj.CraftingOrder, out craftingOrderId)) return;

            // Send to server from client
            NetworkCreateCraftedWeaponInternalServer message = new(
                obj.IsFreeMode,
                craftingHeroId,
                obj.Name,
                cultureId,
                craftingTemplateId,
                obj.WeaponDesign.WeaponName?.ToString() ?? "",
                weaponDesignElementCraftingPieceIds,
                weaponDesignElementScalePercentages,
                weaponModifierId,
                playerHeroId,
                itemModifierGroupId,
                craftingOrderId,
                currentSettlementId
            );
            network.SendAll(message);
        }

        private void CreateCraftedWeaponInternalServer(
            NetworkCreateCraftedWeaponInternalServer obj,
            NetPeer peer)
        {
            GameThread.RunSafe(() => ServerTransactionOutcome.Execute(
                peer, ServerTransactionOutcome.Craft, () =>
            {
                if (!ServerTransactionOutcome.TryResolveOwnedCraftingHero(
                        peer,
                        playerManager,
                        objectManager,
                        obj.CraftingHeroId,
                        out var player,
                        out Hero authorizedHero,
                        out MobileParty playerParty,
                        out string authenticationReason) ||
                    !string.Equals(
                        player.HeroId,
                        obj.PlayerHeroId,
                        StringComparison.Ordinal))
                {
                    RejectSmithing(
                        peer,
                        ServerTransactionOutcome.Craft,
                        authenticationReason);
                    return;
                }
                if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out CraftingCampaignBehavior craftingCampaignBehavior) ||
                    !objectManager.TryGetObjectWithLogging(obj.CraftingTemplateId, out CraftingTemplate craftingTemplate) ||
                    authorizedHero?.PartyBelongedTo == null ||
                    !IsSmithyAvailable(playerParty))
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Craft,
                        "The smithing state is no longer available.");
                    return;
                }
                Hero craftingHero = authorizedHero;
                CultureObject culture = craftingHero.Culture;
                if (!objectManager.TryGetObjectWithLogging(
                        player.HeroId, out Hero playerHero) ||
                    !objectManager.TryGetIdWithLogging(
                        playerParty.CurrentSettlement,
                        out string authoritativeSettlementId))
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Craft,
                        "The player smithing context could not be resolved.");
                    return;
                }
                string cultureId = null;
                if (culture != null &&
                    !objectManager.TryGetIdWithLogging(culture, out cultureId))
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Craft,
                        "The crafting culture could not be resolved.");
                    return;
                }

                ItemModifierGroup itemModifierGroup =
                    craftingTemplate.ItemModifierGroup;

                if (!TryGetAuthorizedUsedPieces(
                        player.HeroId,
                        craftingTemplate,
                        obj.WeaponDesignElementCraftingPieceIds,
                        obj.WeaponDesignElementScalePercentages,
                        out WeaponDesignElement[] usedPieces))
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Craft,
                        "The selected weapon pieces are invalid or not unlocked.");
                    return;
                }

                Settlement currentSettlement = null;
                CraftingOrder craftingOrder = null;
                if (!obj.IsFreeMode &&
                    !TryResolveExactCraftingOrder(
                        craftingCampaignBehavior,
                        craftingHero.PartyBelongedTo,
                        craftingTemplate,
                        authoritativeSettlementId,
                        obj.CraftingOrderId,
                        out currentSettlement,
                        out craftingOrder))
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Craft,
                        "The selected crafting order is no longer available in this town.");
                    return;
                }

                // Replace original TaleWorlds implementation

                string nextCraftedItemId = craftingCampaignBehavior.GetNextCraftedItemId();
                WeaponDesign weaponDesign = new WeaponDesign(craftingTemplate, new TextObject(obj.WeaponName), usedPieces);
                if (obj.IsFreeMode)
                {
                    weaponDesign = new WeaponDesign(weaponDesign.Template, weaponDesign.WeaponName, weaponDesign.UsedPieces, nextCraftedItemId);
                }
                ItemModifier weaponModifier = obj.IsFreeMode
                    ? Campaign.Current.Models.SmithingModel
                        .GetCraftedWeaponModifier(weaponDesign, craftingHero)
                    : null;
                string weaponModifierId = string.Empty;
                if (weaponModifier != null &&
                    !objectManager.TryGetIdWithLogging(
                        weaponModifier, out weaponModifierId))
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Craft,
                        "The server could not resolve the crafted weapon quality.");
                    return;
                }
                string itemModifierGroupId = string.Empty;
                if (itemModifierGroup != null &&
                    !objectManager.TryGetIdWithLogging(
                        itemModifierGroup, out itemModifierGroupId))
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Craft,
                        "The server could not resolve the smithing modifier group.");
                    return;
                }

                // Implement CraftingCampaignBehavior.SpendMaterials(weaponDesign) here as it needs the party roster, MainParty on server won't be correct
                ItemRoster itemRoster = craftingHero.PartyBelongedTo.ItemRoster;
                int[] smithingCostsForWeaponDesign = Campaign.Current.Models.SmithingModel.GetSmithingCostsForWeaponDesign(weaponDesign);
                if (!HasSmithingMaterials(
                        itemRoster, smithingCostsForWeaponDesign))
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Craft,
                        "The required smithing materials are no longer available.");
                    return;
                }

                ItemObject craftedItemObject = new ItemObject();
                using (new AllowedThread())
                {
                    ItemObject.InitAsPlayerCraftedItem(ref craftedItemObject);
                    Crafting.GenerateItem(
                        weaponDesign,
                        obj.Name,
                        culture,
                        itemModifierGroup,
                        ref craftedItemObject,
                        nextCraftedItemId);
                }

                int energyCostForSmithing = Campaign.Current.Models.SmithingModel.GetEnergyCostForSmithing(craftedItemObject, craftingHero);
                int currentStamina = craftingCampaignBehavior.GetHeroCraftingStamina(craftingHero);
                if (energyCostForSmithing < 0 || currentStamina < energyCostForSmithing)
                {
                    RejectSmithing(peer, ServerTransactionOutcome.Craft,
                        "The selected hero does not have enough smithing stamina.");
                    return;
                }

                ItemRosterElement[] itemRosterBefore = itemRoster.ToArray();
                bool mapped = false;
                bool registered = false;
                int newHeroCraftingStamina =
                    currentStamina - energyCostForSmithing;
                try
                {
                    for (int i = 8; i >= 0; i--)
                    {
                        if (smithingCostsForWeaponDesign[i] != 0)
                            itemRoster.AddToCounts(
                                Campaign.Current.Models.SmithingModel
                                    .GetCraftingMaterialItem(
                                        (CraftingMaterials)i),
                                smithingCostsForWeaponDesign[i]);
                    }

                    objectManager.AddExisting(
                        nextCraftedItemId, craftedItemObject);
                    mapped = true;
                    MBObjectManager.Instance.RegisterObject<ItemObject>(
                        craftedItemObject);
                    registered = true;

                    if (obj.IsFreeMode)
                    {
                        if (weaponModifier == null)
                            itemRoster.AddToCounts(craftedItemObject, 1);
                        else
                            itemRoster.AddToCounts(
                                new EquipmentElement(
                                    craftedItemObject,
                                    weaponModifier,
                                    null,
                                    false),
                                1);
                    }
                    craftingCampaignBehavior.SetHeroCraftingStamina(
                        craftingHero, newHeroCraftingStamina);
                    if (!obj.IsFreeMode)
                    {
                        // Complete the order and its gold transfer inside this
                        // same reversible craft commit.
                        messageBroker.Publish(this, new CompleteOrderServer(
                            currentSettlement.Town,
                            craftingOrder,
                            craftedItemObject,
                            craftingHero,
                            playerHero));
                    }
                }
                catch (Exception exception)
                {
                    try
                    {
                        craftingCampaignBehavior.SetHeroCraftingStamina(
                            craftingHero, currentStamina);
                        itemRoster.Clear();
                        itemRoster.Add(itemRosterBefore);
                        if (registered)
                            MBObjectManager.Instance.UnregisterObject(
                                craftedItemObject);
                        if (mapped)
                            objectManager.Remove(craftedItemObject);
                    }
                    catch (Exception rollbackException)
                    {
                        Logger.Error(
                            rollbackException,
                            "Smithing rollback failed for {ItemId}",
                            nextCraftedItemId);
                    }
                    Logger.Error(
                        exception,
                        "Smithing core commit failed for {ItemId}",
                        nextCraftedItemId);
                    RejectSmithing(
                        peer,
                        ServerTransactionOutcome.Craft,
                        "The weapon could not be committed safely.");
                    return;
                }

                // Core state is committed. Each notification is best-effort and
                // cannot make this same transaction retry the material spend.
                TryPostCommit(() => network.SendAll(
                    new NetworkSetHeroCraftingStamina(
                        obj.CraftingHeroId,
                        newHeroCraftingStamina)), "crafting stamina");
                TryPostCommit(() => CampaignEventDispatcher.Instance
                    .OnNewItemCrafted(
                        craftedItemObject,
                        weaponModifier,
                        !obj.IsFreeMode), "crafted-item event");
                TryPostCommit(() =>
                {
                    int researchGain = Campaign.Current.Models.SmithingModel
                        .GetPartResearchGainForSmithingItem(
                            craftedItemObject, craftingHero, obj.IsFreeMode);
                    CraftingCampaignBehaviorResearchPointHandler
                        .AllowAuthoritativeResearch(
                            peer,
                            player.HeroId,
                            obj.CraftingTemplateId,
                            researchGain);
                }, "crafting research permit");
                TryPostCommit(() =>
                {
                    float craftingXp = obj.IsFreeMode
                        ? Campaign.Current.Models.SmithingModel
                            .GetSkillXpForSmithingInFreeBuildMode(
                                craftedItemObject)
                        : craftingOrder.GetOrderExperience(
                                craftedItemObject,
                                craftingCampaignBehavior
                                    .GetCurrentItemModifier()) +
                            Campaign.Current.Models.SmithingModel
                                .GetSkillXpForSmithingInCraftingOrderMode(
                                    craftedItemObject);
                    craftingHero.AddSkillXp(
                        DefaultSkills.Crafting, craftingXp);
                }, "crafting skill XP");
                var sanitizedRequest = new NetworkCreateCraftedWeaponInternalServer(
                    obj.IsFreeMode,
                    obj.CraftingHeroId,
                    obj.Name,
                    cultureId,
                    obj.CraftingTemplateId,
                    obj.WeaponName,
                    obj.WeaponDesignElementCraftingPieceIds,
                    obj.WeaponDesignElementScalePercentages,
                    weaponModifierId,
                    player.HeroId,
                    itemModifierGroupId,
                    obj.IsFreeMode ? null : obj.CraftingOrderId,
                    authoritativeSettlementId);
                TryPostCommit(() => network.SendAll(
                    new NetworkCreateCraftedWeaponInternalClients(
                        sanitizedRequest, nextCraftedItemId)),
                    "crafted-item broadcast");
                TryPostCommit(() =>
                {
                    IReadOnlyList<string> history =
                        sessionCraftingPlayerDataInterface
                            .AppendCraftingHistory(
                                player.HeroId, nextCraftedItemId);
                    network.SendAll(new NetworkUpdateCraftedItemHistory(
                        player.HeroId, history.ToList()));
                }, "crafted-item history");
                ServerTransactionOutcome.AllowCraftRename(
                    peer, nextCraftedItemId);
                ServerTransactionOutcome.AllowCraftXp(peer, obj.CraftingHeroId);
                ServerTransactionOutcome.Accept(
                    peer, ServerTransactionOutcome.Craft);
            }));
        }

        private void CreateCraftedWeaponInternalClients(NetworkCreateCraftedWeaponInternalClients obj)
        {
            GameThread.RunSafe(() =>
            {
                using (new AllowedThread())
                {
                    if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out CraftingCampaignBehavior craftingCampaignBehavior)) return;
                    if (!objectManager.TryGetObjectWithLogging(obj.CraftingTemplateId, out CraftingTemplate craftingTemplate)) return;
                    if (!objectManager.TryGetObjectWithLogging(obj.PlayerHeroId, out Hero playerHero)) return;
                    if (!objectManager.TryGetObjectWithLogging(obj.CraftingHeroId, out Hero craftingHero)) return;
                    if (!objectManager.TryGetObjectWithLogging(obj.CurrentSettlementId, out Settlement currentSettlement)) return;

                    ItemModifierGroup itemModifierGroup = null;
                    if (obj.ItemModifierGroupId != null && !objectManager.TryGetObjectWithLogging(obj.ItemModifierGroupId, out itemModifierGroup)) return;

                    if (!GetUsedPieces(obj.WeaponDesignElementCraftingPieceIds, obj.WeaponDesignElementScalePercentages, out WeaponDesignElement[] usedPieces)) return;

                    ItemModifier weaponModifier = null;
                    if (obj.WeaponModifierId != "" && !objectManager.TryGetObjectWithLogging(obj.WeaponModifierId, out weaponModifier)) return;

                    CultureObject culture = null;
                    if (obj.CultureId != null && !objectManager.TryGetObjectWithLogging(obj.CultureId, out culture)) return;

                    CraftingOrder craftingOrder = null;
                    if (obj.CraftingOrderId != null && !objectManager.TryGetObjectWithLogging(obj.CraftingOrderId, out craftingOrder)) return;

                    // Replace original TaleWorlds implementation
                    string nextCraftedItemId = obj.NextCraftedItemId;
                    WeaponDesign weaponDesign = new WeaponDesign(craftingTemplate, new TextObject(obj.WeaponName), usedPieces);
                    if (obj.IsFreeMode)
                    {
                        weaponDesign = new WeaponDesign(weaponDesign.Template, weaponDesign.WeaponName, weaponDesign.UsedPieces, nextCraftedItemId);
                    }

                    ItemObject craftedItemObject = craftingCampaignBehaviorInterface
                        .CreateAndRegisterCraftedItem(
                            weaponDesign,
                            obj.Name,
                            culture,
                            itemModifierGroup,
                            nextCraftedItemId);
                    CampaignEventDispatcher.Instance.OnNewItemCrafted(
                        craftedItemObject, weaponModifier, !obj.IsFreeMode);

                    if (playerHero == Hero.MainHero)
                    {
                        if (GameStateManager.Current.ActiveState is CraftingState currentState)
                            currentState.CraftingLogic._craftedItemObject = craftedItemObject;

                        messageBroker.Publish(this, new UpdateCraftedItem(craftedItemObject));
                        AddItemToHistoryPatch.OverrideAddItemToHistory(ref craftingCampaignBehavior, craftedItemObject);
                    }

                    if (obj.IsFreeMode &&
                        objectManager.TryGetIdWithLogging(craftedItemObject, out string craftedItemId))
                    {
                        network.SendAll(new NetworkAddCraftedItemToRoster(
                            craftedItemId,
                            obj.PlayerHeroId,
                            obj.WeaponModifierId));
                    }
                }
            });
        }

        private void AddCraftedItemToRoster(NetworkAddCraftedItemToRoster obj)
        {
            GameThread.RunSafe(() =>
            {
                if (!objectManager.TryGetObjectWithLogging(
                        obj.CraftedItemId, out ItemObject craftedItemObject) ||
                    !objectManager.TryGetObjectWithLogging(
                        obj.PlayerHeroId, out Hero playerHero))
                    return;

                ItemModifier weaponModifier = null;
                if (obj.WeaponModifierId != "" &&
                    !objectManager.TryGetObjectWithLogging(
                        obj.WeaponModifierId, out weaponModifier))
                    return;

                if (playerHero.PartyBelongedTo?.ItemRoster
                        .FindIndexOfItem(craftedItemObject) == -1)
                {
                    craftingCampaignBehaviorInterface.AddCraftedItemToRoster(
                        playerHero.PartyBelongedTo.ItemRoster,
                        weaponModifier,
                        craftedItemObject);
                }
            });
        }

        private bool GetUsedPieces(List<string> craftingPieceIds, List<int> scalePercentages, out WeaponDesignElement[] usedPieces)
        {
            usedPieces = null;
            List<WeaponDesignElement> usedPiecesList = new();
            for (int i = 0; i < craftingPieceIds.Count; i++)
            {
                if (craftingPieceIds[i] == "")
                {
                    usedPiecesList.Add(WeaponDesignElement.GetInvalidPieceForType((CraftingPiece.PieceTypes)i));
                    continue;
                }

                if (!objectManager.TryGetObjectWithLogging(craftingPieceIds[i], out CraftingPiece currentCraftingPiece)) return false;

                usedPiecesList.Add(new WeaponDesignElement(currentCraftingPiece, scalePercentages[i]));
            }

            usedPieces = usedPiecesList.ToArray();
            return true;
        }

        private bool TryGetAuthorizedUsedPieces(
            string playerHeroId,
            CraftingTemplate template,
            List<string> craftingPieceIds,
            List<int> scalePercentages,
            out WeaponDesignElement[] usedPieces)
        {
            usedPieces = null;
            int pieceTypeCount =
                (int)CraftingPiece.PieceTypes.NumberOfPieceTypes;
            if (template == null || string.IsNullOrEmpty(playerHeroId) ||
                craftingPieceIds == null || scalePercentages == null ||
                craftingPieceIds.Count != pieceTypeCount ||
                scalePercentages.Count != pieceTypeCount)
                return false;

            var opened = new HashSet<string>(
                sessionCraftingPlayerDataInterface.GetOpenedCraftingPieces(
                    playerHeroId, template.StringId) ??
                Array.Empty<string>(),
                StringComparer.Ordinal);
            var result = new WeaponDesignElement[pieceTypeCount];
            for (int i = 0; i < pieceTypeCount; i++)
            {
                CraftingPiece.PieceTypes expectedType =
                    (CraftingPiece.PieceTypes)i;
                string pieceId = craftingPieceIds[i];
                int scale = scalePercentages[i];
                if (!template.IsPieceTypeUsable(expectedType))
                {
                    if (!string.IsNullOrEmpty(pieceId) || scale != -1)
                        return false;
                    result[i] = WeaponDesignElement.GetInvalidPieceForType(
                        expectedType);
                    continue;
                }

                if (string.IsNullOrEmpty(pieceId) || scale < 90 || scale > 110 ||
                    !objectManager.TryGetObject(
                        pieceId, out CraftingPiece piece) || piece == null ||
                    !piece.IsValid || piece.PieceType != expectedType ||
                    !template.Pieces.Contains(piece) ||
                    piece.IsHiddenOnDesigner && !piece.IsGivenByDefault ||
                    !piece.IsGivenByDefault && !opened.Contains(pieceId) ||
                    piece.FullScale && scale != 100)
                    return false;
                result[i] = new WeaponDesignElement(piece, scale);
            }

            usedPieces = result;
            return true;
        }

        private bool TryResolveExactCraftingOrder(
            CraftingCampaignBehavior behavior,
            MobileParty party,
            CraftingTemplate template,
            string settlementId,
            string craftingOrderId,
            out Settlement settlement,
            out CraftingOrder craftingOrder)
        {
            settlement = null;
            craftingOrder = null;
            if (string.IsNullOrEmpty(settlementId) ||
                string.IsNullOrEmpty(craftingOrderId) ||
                !objectManager.TryGetObject(settlementId, out settlement) ||
                !objectManager.TryGetObject(craftingOrderId, out craftingOrder) ||
                settlement != party?.CurrentSettlement ||
                settlement?.Town == null || behavior == null || template == null ||
                craftingOrder?.WeaponDesignTemplate?.Template != template ||
                !behavior.CraftingOrders.TryGetValue(
                    settlement.Town,
                    out CraftingCampaignBehavior.CraftingOrderSlots slots) ||
                slots == null)
                return false;

            CraftingOrder resolvedOrder = craftingOrder;
            return slots.Slots.Concat(slots.CustomOrders)
                .Any(order => ReferenceEquals(order, resolvedOrder));
        }

        private static void TryPostCommit(Action action, string operation)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception exception)
            {
                Logger.Error(
                    exception,
                    "Smithing committed, but {Operation} failed",
                    operation);
            }
        }

        private static void RestoreSmithingCore(
            ItemRoster itemRoster,
            ItemRosterElement[] itemRosterBefore,
            CraftingCampaignBehavior behavior,
            Hero craftingHero,
            int staminaBefore,
            Exception commitException,
            string operation)
        {
            try
            {
                itemRoster.Clear();
                itemRoster.Add(itemRosterBefore);
                behavior.SetHeroCraftingStamina(
                    craftingHero, staminaBefore);
            }
            catch (Exception rollbackException)
            {
                Logger.Error(
                    rollbackException,
                    "Smithing rollback failed during {Operation}",
                    operation);
            }
            Logger.Error(
                commitException,
                "Smithing core commit failed during {Operation}",
                operation);
        }

        private static bool RefiningFormulasMatch(
            Crafting.RefiningFormula left,
            Crafting.RefiningFormula right)
        {
            return left.Input1 == right.Input1 &&
                left.Input1Count == right.Input1Count &&
                left.Input2 == right.Input2 &&
                left.Input2Count == right.Input2Count &&
                left.Output == right.Output &&
                left.OutputCount == right.OutputCount &&
                left.Output2 == right.Output2 &&
                left.Output2Count == right.Output2Count;
        }

        private static void AddRequiredMaterial(
            IDictionary<CraftingMaterials, int> required,
            CraftingMaterials material,
            int amount)
        {
            if (amount <= 0)
                return;
            required.TryGetValue(material, out int current);
            required[material] = current + amount;
        }

        private static bool HasSmithingMaterials(
            ItemRoster itemRoster,
            int[] costs)
        {
            if (itemRoster == null || costs == null || costs.Length < 9)
                return false;
            for (int i = 0; i <= 8; i++)
            {
                int required = -costs[i];
                if (required <= 0)
                    continue;
                ItemObject material = Campaign.Current.Models.SmithingModel
                    .GetCraftingMaterialItem((CraftingMaterials)i);
                if (itemRoster.GetItemNumber(material) < required)
                    return false;
            }
            return true;
        }

        private static void RejectSmithing(
            NetPeer peer, int kind, string reason)
        {
            CraftingCampaignBehaviorResearchPointHandler
                .DiscardPendingResearch(peer);
            ServerTransactionOutcome.Reject(peer, kind, reason);
        }

        private static bool IsSmithyAvailable(MobileParty playerParty)
            => playerParty?.IsActive == true &&
               playerParty.MapEvent == null &&
               playerParty.CurrentSettlement?.Town != null;

        private void SetHeroCraftingStaminaClients(NetworkSetHeroCraftingStamina obj)
        {
            GameThread.RunSafe(() =>
            {
                if (!craftingCampaignBehaviorInterface.TryGetCraftingBehavior(out CraftingCampaignBehavior craftingCampaignBehavior)) return;
                if (!objectManager.TryGetObjectWithLogging(obj.CraftingHeroId, out Hero craftingHero)) return;

                craftingCampaignBehavior.GetRecordForCompanion(craftingHero).CraftingStamina = MathF.Max(0, obj.Value);
            });
        }
    }
}
