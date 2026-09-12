using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Buildings.Messages;
using GameInterface.Services.Buildings.Patches;
using GameInterface.Services.ObjectManager;
using Helpers;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Buildings.Handlers;

internal class BuildingHelperHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BuildingHelperHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public BuildingHelperHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<DefaultBuildingChanged>(Handle_DefaultBuildingChanged);
        messageBroker.Subscribe<ChangeDefaultBuilding>(Handle_ChangeDefaultBuilding);

        messageBroker.Subscribe<CurrentBuildingQueueChanged>(Handle_CurrentBuildingQueueChanged);
        messageBroker.Subscribe<ChangeCurrentBuildingQueue>(Handle_ChangeCurrentBuildingQueue);

        messageBroker.Subscribe<BuildingProcessBoostedWithGold>(Handle_BuildingProcessBoostedWithGold);
        messageBroker.Subscribe<BoostBuildingProcessWithGold>(Handle_BoostBuildingProcessWithGold);

        messageBroker.Subscribe<RefreshPlayerSettlementManagementVM>(Handle_RefreshPlayerSettlementManagementVM);
        messageBroker.Subscribe<NetworkRefreshPlayerSettlementManagementVM>(Handle_NetworkRefreshPlayerSettlementManagementVM);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<DefaultBuildingChanged>(Handle_DefaultBuildingChanged);
        messageBroker.Unsubscribe<ChangeDefaultBuilding>(Handle_ChangeDefaultBuilding);

        messageBroker.Unsubscribe<CurrentBuildingQueueChanged>(Handle_CurrentBuildingQueueChanged);
        messageBroker.Unsubscribe<ChangeCurrentBuildingQueue>(Handle_ChangeCurrentBuildingQueue);

        messageBroker.Unsubscribe<BuildingProcessBoostedWithGold>(Handle_BuildingProcessBoostedWithGold);
        messageBroker.Unsubscribe<BoostBuildingProcessWithGold>(Handle_BoostBuildingProcessWithGold);

        messageBroker.Unsubscribe<RefreshPlayerSettlementManagementVM>(Handle_RefreshPlayerSettlementManagementVM);
        messageBroker.Unsubscribe<NetworkRefreshPlayerSettlementManagementVM>(Handle_NetworkRefreshPlayerSettlementManagementVM);
    }

    private void Handle_DefaultBuildingChanged(MessagePayload<DefaultBuildingChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.NewDefault, out var newDefaultId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Town, out var townId)) return;

        var message = new ChangeDefaultBuilding(newDefaultId, townId);
        network.SendAll(message);
    }

    private void Handle_ChangeDefaultBuilding(MessagePayload<ChangeDefaultBuilding> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Building>(data.NewDefaultId, out var newDefault)) return;
            if (!objectManager.TryGetObjectWithLogging<Town>(data.TownId, out var town)) return;

            BuildingHelper.ChangeDefaultBuilding(newDefault, town);
        });
    }

    private void Handle_CurrentBuildingQueueChanged(MessagePayload<CurrentBuildingQueueChanged> obj)
    {
        var buildingIds = new List<string>();
        foreach (var building in obj.What.Buildings)
        {
            if (!objectManager.TryGetIdWithLogging(building, out var currentBuildingId)) continue;

            buildingIds.Add(currentBuildingId);
        }

        if (!objectManager.TryGetIdWithLogging(obj.What.Town, out var townId)) return;

        var message = new ChangeCurrentBuildingQueue(buildingIds, townId);
        network.SendAll(message);
    }

    private void Handle_ChangeCurrentBuildingQueue(MessagePayload<ChangeCurrentBuildingQueue> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Town>(data.TownId, out var town)) return;

            var buildings = new List<Building>();
            if (data.BuildingIds != null)
            {
                foreach (var buildingId in data.BuildingIds)
                {
                    if (!objectManager.TryGetObjectWithLogging<Building>(buildingId, out var currentBuilding)) continue;

                    // Reject adding buildings to queue that are already max level
                    if (currentBuilding.CurrentLevel == 3) continue;

                    buildings.Add(currentBuilding);
                }
            }

            BuildingHelper.ChangeCurrentBuildingQueue(buildings, town);

            messageBroker.Publish(this, new RefreshPlayerSettlementManagementVM(town));
        });
    }

    private void Handle_BuildingProcessBoostedWithGold(MessagePayload<BuildingProcessBoostedWithGold> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Town, out var townId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;

        var message = new BoostBuildingProcessWithGold(obj.What.Gold, townId, heroId);
        network.SendAll(message);
    }

    private void Handle_BoostBuildingProcessWithGold(MessagePayload<BoostBuildingProcessWithGold> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Town>(data.TownId, out var town)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.HeroId, out var hero)) return;

            int difference = 0;
            if (data.Gold < town.BoostBuildingProcess)
            {
                difference = town.BoostBuildingProcess - data.Gold;
                GiveGoldAction.ApplyBetweenCharacters(null, hero, difference, false);
            }
            else if (data.Gold > town.BoostBuildingProcess)
            {
                difference = data.Gold - town.BoostBuildingProcess;
                GiveGoldAction.ApplyBetweenCharacters(hero, null, difference, false);
            }
            town.BoostBuildingProcess = data.Gold;
        });
    }

    private void Handle_RefreshPlayerSettlementManagementVM(MessagePayload<RefreshPlayerSettlementManagementVM> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Town, out var townId)) return;

        var message = new NetworkRefreshPlayerSettlementManagementVM(townId);
        network.SendAll(message);
    }

    private void Handle_NetworkRefreshPlayerSettlementManagementVM(MessagePayload<NetworkRefreshPlayerSettlementManagementVM> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Town>(data.TownId, out var town)) return;

            // Client isn't in the settlement management screen
            if (TownManagementViewPatches.Current?._dataSource == null) return;

            // Client is in the settlement management screen but not looking at the updated town
            if (TownManagementViewPatches.Current._dataSource._settlement != town.Settlement) return;

            TownManagementViewPatches.Current._dataSource._projectSelection?.Refresh();

            TownManagementViewPatches.Current._dataSource.RefreshCurrentDevelopment();
            TownManagementViewPatches.Current._dataSource.RefreshTownManagementStats();
        });
    }
}