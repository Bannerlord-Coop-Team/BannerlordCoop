using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Buildings.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Notifications.Messages;
using Helpers;
using LiteNetLib;
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
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<DefaultBuildingChanged>(Handle_DefaultBuildingChanged);
        messageBroker.Unsubscribe<ChangeDefaultBuilding>(Handle_ChangeDefaultBuilding);
        messageBroker.Unsubscribe<CurrentBuildingQueueChanged>(Handle_CurrentBuildingQueueChanged);
        messageBroker.Unsubscribe<ChangeCurrentBuildingQueue>(Handle_ChangeCurrentBuildingQueue);
        messageBroker.Unsubscribe<BuildingProcessBoostedWithGold>(Handle_BuildingProcessBoostedWithGold);
        messageBroker.Unsubscribe<BoostBuildingProcessWithGold>(Handle_BoostBuildingProcessWithGold);
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
        if (!objectManager.TryGetObjectWithLogging<Building>(obj.What.NewDefaultId, out var newDefault)) return;
        if (!objectManager.TryGetObjectWithLogging<Town>(obj.What.TownId, out var town)) return;

        BuildingHelper.ChangeDefaultBuilding(newDefault, town);
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
        var buildings = new List<Building>();
        if (obj.What.BuildingIds != null)
        {
            foreach (var buildingId in obj.What.BuildingIds)
            {
                if (!objectManager.TryGetObjectWithLogging<Building>(buildingId, out var currentBuilding)) continue;

                buildings.Add(currentBuilding);
            }
        }

        if (!objectManager.TryGetObjectWithLogging<Town>(obj.What.TownId, out var town)) return;

        BuildingHelper.ChangeCurrentBuildingQueue(buildings, town);
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
        if (!objectManager.TryGetObjectWithLogging<Town>(obj.What.TownId, out var town)) return;
        if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;

        int difference = 0;
        if (obj.What.Gold < town.BoostBuildingProcess)
        {
            difference = town.BoostBuildingProcess - obj.What.Gold;
            GiveGoldAction.ApplyBetweenCharacters(null, hero, difference, false);
        }
        else if (obj.What.Gold > town.BoostBuildingProcess)
        {
            difference = obj.What.Gold - town.BoostBuildingProcess;
            GiveGoldAction.ApplyBetweenCharacters(hero, null, difference, false);
        }
        town.BoostBuildingProcess = obj.What.Gold;

        network.Send(obj.Who as NetPeer, new NotifyGoldChange(-difference));
    }
}