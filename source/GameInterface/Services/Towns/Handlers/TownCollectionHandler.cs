using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Towns.Messages.Collections;
using GameInterface.Utils;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Towns.Handlers
{
    /// <summary>
    /// Handles TownState Changes (e.g. Prosperity, Governor, etc.).
    /// </summary>
    public class TownCollectionHandler : GenericHandler<Town, TownCollectionHandler>
    {
        public TownCollectionHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network) : base(messageBroker, objectManager, network)
        {
            Subscribe<Workshop[], WorkshopsSet>(WorkshopsSetHandler);
            SubscribeNetwork<Workshop[], NetworkWorkshopsSet>(NetworkWorkshopsSetHandler);

            Subscribe<Workshop, WorkshopsChanged>(WorkshopsChangedHandler);
            SubscribeNetwork<Workshop, NetworkWorkshopsChanged>(NetworkWorkshopsChangedHandler);

            SubscribeGenericReference<Building, BuildingsAdded, NetworkBuildingsAdded>();
            SubscribeNetworkReference<Building, NetworkBuildingsAdded>((instance,value,_) => { instance.Buildings.Add(value); });

            SubscribeGenericReference<Building, BuildingsRemoved, NetworkBuildingsRemoved>();
            SubscribeNetworkReference<Building, NetworkBuildingsRemoved>((instance, value, _) => { instance.Buildings.Remove(value); });

            Subscribe<Queue<Building>, BuildingsInProgressSet>(BuildingsInProgressSetHandler);
            SubscribeNetwork<Queue<Building>, NetworkBuildingsInProgressSet>(NetworkBuildingsInProgressSetHandler);

            SubscribeGenericReference<Building, BuildingsInProgressAdded, NetworkBuildingsInProgressAdded>();
            SubscribeNetworkReference<Building, NetworkBuildingsInProgressAdded>((instance, value, _) => { instance.BuildingsInProgress.Enqueue(value); });

            SubscribeGenericReference<Building, BuildingsInProgressRemoved, NetworkBuildingsInProgressRemoved>();
            SubscribeNetworkReference<Building, NetworkBuildingsInProgressRemoved>((instance, _, _) => { instance.BuildingsInProgress.Dequeue(); });

            SubscribeGenericReference<Village, TradeBoundVillagesCacheAdded, NetworkTradeBoundVillagesCacheAdded>();
            SubscribeNetworkReference<Village, NetworkTradeBoundVillagesCacheAdded>((instance, value, _) => { instance.TradeBoundVillages.Add(value); });

            SubscribeGenericReference<Village, TradeBoundVillagesCacheRemoved, NetworkTradeBoundVillagesCacheRemoved>();
            SubscribeNetworkReference<Village, NetworkTradeBoundVillagesCacheRemoved>((instance, value, _) => { instance.TradeBoundVillages.Remove(value); });
        }

        private void WorkshopsSetHandler(string instanceId, WorkshopsSet data)
        {
            var workshopIds = new List<(int index, string id)>();
            for (int i = 0; i < data.Value.Length; i++)
            {
                if (!TryGetId(data.Value[i], out string workshopId)) continue;
                workshopIds.Add((i, workshopId));
            }
            network.SendAll(new NetworkWorkshopsSet(instanceId, workshopIds.ToArray(), data.Value.Length));
        }

        private void NetworkWorkshopsSetHandler(Town town, NetworkWorkshopsSet data)
        {
            town.Workshops = new Workshop[data.Length];
            if(data.WorkshopIds != null)
            { 
                for (int i = 0; i < data.WorkshopIds.Length; i++)
                {
                    var workshopMapping = data.WorkshopIds[i];
                    if (!objectManager.TryGetObject(workshopMapping.id, out Workshop workshop)) continue;
                    town.Workshops[workshopMapping.index] = workshop;
                }
            }
        }

        private void WorkshopsChangedHandler(string instanceId, WorkshopsChanged data)
        {
            if (!TryGetId(data.Value, out string workshopId)) return;
            network.SendAll(new NetworkWorkshopsChanged(instanceId, workshopId, data.Index));
        }

        private void NetworkWorkshopsChangedHandler(Town town, NetworkWorkshopsChanged data)
        {
            if (!objectManager.TryGetObject(data.WorkshopId, out Workshop workshop) && !string.IsNullOrWhiteSpace(data.WorkshopId)) return;
            town.Workshops[data.Index] = workshop;
        }

        private void BuildingsInProgressSetHandler(string instanceId, BuildingsInProgressSet data)
        {
            var buildings = data.Value.ToList();
            var buildingIds = new List<string>();
            for (int i = 0; i < buildings.Count; i++)
            {
                if (!TryGetId(buildings[i], out string buildingId)) return;
                buildingIds.Add(buildingId);
            }

            network.SendAll(new NetworkBuildingsInProgressSet(instanceId, buildingIds));
        }

        private void NetworkBuildingsInProgressSetHandler(Town town, NetworkBuildingsInProgressSet data)
        {
            town.BuildingsInProgress = new Queue<Building>();
            // Empty lists are transmitted as null
            if(data.BuildingIds != null)
            { 
                for(int i = 0; i < data.BuildingIds.Count; i++)
                {
                    if (!objectManager.TryGetObject(data.BuildingIds[i], out Building building)) return;
                    town.BuildingsInProgress.Enqueue(building);
                }
            }
        }
    }
}