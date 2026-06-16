using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Buildings.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Notifications.Messages;
using Helpers;
using LiteNetLib;
using Serilog;
using System;
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
        var data = obj.What;

        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<Building>(data.NewDefaultId, out var newDefault)) return;
                if (!objectManager.TryGetObjectWithLogging<Town>(data.TownId, out var town)) return;

                BuildingHelper.ChangeDefaultBuilding(newDefault, town);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(ChangeDefaultBuilding));
            }
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

        GameThread.Run(() =>
        {
            try
            {
                var buildings = new List<Building>();
                if (data.BuildingIds != null)
                {
                    foreach (var buildingId in data.BuildingIds)
                    {
                        if (!objectManager.TryGetObjectWithLogging<Building>(buildingId, out var currentBuilding)) continue;

                        buildings.Add(currentBuilding);
                    }
                }

                if (!objectManager.TryGetObjectWithLogging<Town>(data.TownId, out var town)) return;

                BuildingHelper.ChangeCurrentBuildingQueue(buildings, town);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(ChangeCurrentBuildingQueue));
            }
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
        var peer = obj.Who as NetPeer;

        GameThread.Run(() =>
        {
            try
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

                network.Send(peer, new NotifyGoldChange(-difference));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(BoostBuildingProcessWithGold));
            }
        });
    }
}