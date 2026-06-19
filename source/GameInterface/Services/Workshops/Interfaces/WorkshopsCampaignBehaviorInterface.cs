using Common;
using Common.Logging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.Settlements.Workshops.WorkshopType;

namespace GameInterface.Services.Workshops.Interfaces;

public interface IWorkshopsCampaignBehaviorInterface : IGameAbstraction
{
    void RunTownWorkshop(Town townComponent, Workshop workshop);

    bool TickOneProductionCycleForPlayerWorkshop(Production production, Workshop workshop, bool effectCapital);
}

internal class WorkshopsCampaignBehaviorInterface : IWorkshopsCampaignBehaviorInterface
{
    static readonly ILogger Logger = LogManager.GetLogger<WorkshopsCampaignBehaviorInterface>();
    private readonly ISessionWorkshopPlayerDataInterface sessionWorkshopPlayerDataInterface;
    private readonly IObjectManager objectManager;

    public WorkshopsCampaignBehaviorInterface(ISessionWorkshopPlayerDataInterface sessionWorkshopPlayerDataInterface, IObjectManager objectManager)
    {
        this.sessionWorkshopPlayerDataInterface = sessionWorkshopPlayerDataInterface;
        this.objectManager = objectManager;
    }

    public void RunTownWorkshop(Town townComponent, Workshop workshop)
    {
        GameThread.Run(() =>
        {
            try
            {
                RunTownWorkshopInternal(townComponent, workshop);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to run {method}", nameof(TickOneProductionCycleForPlayerWorkshop));
            }
        });
    }

    public bool TickOneProductionCycleForPlayerWorkshop(Production production, Workshop workshop, bool effectCapital)
    {
        var result = false;
        GameThread.Run(() =>
        {
            try
            {
                result = TickOneProductionCycleForPlayerWorkshopInternal(production, workshop, effectCapital);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to run {method}", nameof(TickOneProductionCycleForPlayerWorkshop));
                result = false;
            }
        });
        return result;
    }

    private void RunTownWorkshopInternal(Town townComponent, Workshop workshop)
    {
        var workshopsBehavior = GetWorkshopsBehavior();

        WorkshopType workshopType = workshop.WorkshopType;
        bool workshopUpdated = false;
        for (int i = 0; i < workshopType.Productions.Count; i++)
        {
            float productionProgress = workshop.GetProductionProgress(i);
            if (productionProgress > 1f)
            {
                productionProgress = 1f;
            }
            productionProgress += Campaign.Current.Models.WorkshopModel.GetEffectiveConversionSpeedOfProduction(workshop, workshopType.Productions[i].ConversionSpeed, false).ResultNumber;
            if (productionProgress >= 1f)
            {
                bool tickedSuccessfully = false;
                while (productionProgress >= 1f)
                {
                    Production production = workshopType.Productions[i];
                    bool availableTradeGood;
                    if (!production.Inputs.Any((ValueTuple<ItemCategory, int> x) => !x.Item1.IsTradeGood))
                    {
                        availableTradeGood = !production.Outputs.Any((ValueTuple<ItemCategory, int> x) => !x.Item1.IsTradeGood);
                    }
                    else
                    {
                        availableTradeGood = false;
                    }
                    tickedSuccessfully = ((workshop.Owner.IsPlayerHero()) ? TickOneProductionCycleForPlayerWorkshop(production, workshop, availableTradeGood) : workshopsBehavior.TickOneProductionCycleForNotableWorkshop(production, workshop, availableTradeGood));
                    if (tickedSuccessfully && availableTradeGood)
                    {
                        workshopUpdated = true;
                    }
                    productionProgress -= 1f;
                }
            }
            workshop.SetProgress(i, productionProgress);
        }
        if (workshopUpdated)
        {
            workshop.UpdateLastRunTime();
        }
    }

    private bool TickOneProductionCycleForPlayerWorkshopInternal(Production production, Workshop workshop, bool effectCapital)
    {
        var workshopsBehavior = GetWorkshopsBehavior();

        bool availableMaterials = false;
        int inputMaterialCost = 0;
        Town town = workshop.Settlement.Town;
        WorkshopsCampaignBehavior.WorkshopData dataOfWorkshop = workshopsBehavior.GetDataOfWorkshop(workshop);
        bool useItemsFromWarehouse = dataOfWorkshop.IsGettingInputsFromWarehouse;
        if (useItemsFromWarehouse)
        {
            if (!GetWarehouseRoster(workshop.Owner, workshop.Settlement, out var warehouseRoster)) return false;

            availableMaterials = workshopsBehavior.DetermineItemRosterHasSufficientInputs(production, warehouseRoster, town, out inputMaterialCost);
            if (availableMaterials)
            {
                inputMaterialCost = 0;
            }
            else
            {
                useItemsFromWarehouse = false;
            }
        }
        if (!availableMaterials)
        {
            availableMaterials = workshopsBehavior.DetermineItemRosterHasSufficientInputs(production, town.Owner.ItemRoster, town, out inputMaterialCost);
        }
        if (availableMaterials)
        {
            List<EquipmentElement> itemsToProduce = workshopsBehavior.GetItemsToProduce(production, workshop, out int outputIncome);
            float num = dataOfWorkshop.StockProductionInWarehouseRatio;
            bool allOutputsWillBeSentToWarehouse = num.ApproximatelyEqualsTo(1f, 1E-05f);
            if (CanPlayerWorkshopProduceThisCycle(production, workshop, inputMaterialCost, outputIncome, effectCapital, allOutputsWillBeSentToWarehouse))
            {
                Dictionary<ItemObject, int> dictionary = new Dictionary<ItemObject, int>();
                foreach (ValueTuple<ItemCategory, int> valueTuple in production.Inputs)
                {
                    if (useItemsFromWarehouse)
                    {
                        workshopsBehavior.ConsumeInputFromWarehouse(valueTuple.Item1, valueTuple.Item2, workshop);
                    }
                    else
                    {
                        workshopsBehavior.ConsumeInputFromTownMarket(valueTuple.Item1, valueTuple.Item2, town, workshop, effectCapital);
                    }
                }
                foreach (EquipmentElement equipmentElement in itemsToProduce)
                {
                    WorkshopsCampaignBehavior.WorkshopData dataOfWorkshop2 = workshopsBehavior.GetDataOfWorkshop(workshop);
                    if (equipmentElement.Item.IsTradeGood && CanItemFitInWarehouse(workshop.Owner, workshop.Settlement, equipmentElement))
                    {
                        workshopsBehavior.AddOutputProgressForWarehouse(workshop, num);
                        if (dataOfWorkshop2.ProductionProgressForWarehouse >= 1f)
                        {
                            workshopsBehavior.ProduceAnOutputToWarehouse(equipmentElement, workshop);
                            workshopsBehavior.AddOutputProgressForWarehouse(workshop, -1f);
                            if (dictionary.ContainsKey(equipmentElement.Item))
                            {
                                Dictionary<ItemObject, int> dictionary2 = dictionary;
                                ItemObject item = equipmentElement.Item;
                                int num2 = dictionary2[item];
                                dictionary2[item] = num2 + 1;
                            }
                            else
                            {
                                dictionary.Add(equipmentElement.Item, 1);
                            }
                        }
                    }
                    else
                    {
                        num = 0f;
                    }
                    workshopsBehavior.AddOutputProgressForTown(workshop, 1f - num);
                    if (dataOfWorkshop2.ProductionProgressForTown >= 1f)
                    {
                        workshopsBehavior.ProduceAnOutputToTown(equipmentElement, workshop, effectCapital);

                        //Replacement for SkillLevelingManager.OnProductionProducedToWarehouse(equipmentElement);
                        workshop.Owner.AddSkillXp(DefaultSkills.Trade, Campaign.Current.Models.WorkshopModel.GetTradeXpPerWarehouseProduction(equipmentElement));

                        workshopsBehavior.AddOutputProgressForTown(workshop, -1f);
                        if (dictionary.ContainsKey(equipmentElement.Item))
                        {
                            Dictionary<ItemObject, int> dictionary3 = dictionary;
                            ItemObject item = equipmentElement.Item;
                            int num2 = dictionary3[item];
                            dictionary3[item] = num2 + 1;
                        }
                        else
                        {
                            dictionary.Add(equipmentElement.Item, 1);
                        }
                    }
                }
                foreach (KeyValuePair<ItemObject, int> keyValuePair in dictionary)
                {
                    ItemObject key = keyValuePair.Key;
                    int value = keyValuePair.Value;
                    CampaignEventDispatcher.Instance.OnItemProduced(key, workshop.Settlement, value);
                }
                return true;
            }
        }
        return false;
    }

    private bool CanItemFitInWarehouse(Hero owner, Settlement settlement, EquipmentElement equipmentElement)
    {
        return GetWarehouseItemRosterWeight(owner, settlement) + equipmentElement.Weight <= (float)Campaign.Current.Models.WorkshopModel.WarehouseCapacity;
    }

    private bool CanPlayerWorkshopProduceThisCycle(Production production, Workshop workshop, int inputMaterialCost, int outputIncome, bool effectCapital, bool allOutputsWillBeSentToWarehouse)
    {
        float num = workshop.WorkshopType.IsHidden ? ((float)inputMaterialCost) : ((float)inputMaterialCost + 200f / production.ConversionSpeed);
        if (Campaign.Current.GameStarted && (float)outputIncome <= num)
        {
            return false;
        }
        if (workshop.Capital < inputMaterialCost)
        {
            return false;
        }
        if (effectCapital)
        {
            bool settlementGoldGreaterThanIncome = workshop.Settlement.Town.Gold >= outputIncome;
            bool isWarehouseAtLimit = !IsWarehouseAtLimit(workshop.Owner, workshop.Settlement);
            if (!settlementGoldGreaterThanIncome && (!allOutputsWillBeSentToWarehouse || isWarehouseAtLimit))
            {
                return false;
            }
        }
        return true;
    }

    private bool GetWarehouseRoster(Hero owner, Settlement settlement, out ItemRoster warehouseRoster)
    {
        warehouseRoster = new();

        if (!objectManager.TryGetIdWithLogging(owner, out var ownerId)) return false;
        if (!objectManager.TryGetIdWithLogging(settlement, out var settlementId)) return false;

        warehouseRoster.Add(sessionWorkshopPlayerDataInterface.GetWarehouseRoster(ownerId, settlementId));
        return true;
    }

    private float GetWarehouseItemRosterWeight(Hero hero, Settlement settlement)
    {
        GetWarehouseRoster(hero, settlement, out var warehouseRoster);
        float num = 0f;
        foreach (ItemRosterElement itemRosterElement in warehouseRoster)
        {
            num += itemRosterElement.GetRosterElementWeight();
        }
        return num;
    }

    private bool IsWarehouseAtLimit(Hero owner, Settlement settlement)
    {
        return GetWarehouseItemRosterWeight(owner, settlement) >= (float)Campaign.Current.Models.WorkshopModel.WarehouseCapacity;
    }

    private WorkshopsCampaignBehavior GetWorkshopsBehavior()
    {
        return Campaign.Current.GetCampaignBehavior<WorkshopsCampaignBehavior>();
    }
}