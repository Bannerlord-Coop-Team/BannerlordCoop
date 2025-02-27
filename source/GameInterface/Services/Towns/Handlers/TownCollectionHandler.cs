using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Towns.Messages.Collections;
using Serilog;
using System;
using System.Collections.Immutable;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Library;

namespace GameInterface.Services.Towns.Handlers
{
    /// <summary>
    /// Handles TownState Changes (e.g. Prosperity, Governor, etc.).
    /// </summary>
    public class TownCollectionHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<TownCollectionHandler>();

        public TownCollectionHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<WorkshopsSet>(HandleWorkShopsSet);
            messageBroker.Subscribe<NetworkWorkshopsSet>(HandleWorkShopsSet);

            messageBroker.Subscribe<WorkshopsChanged>(HandleWorkShopsChanged);
            messageBroker.Subscribe<NetworkWorkshopsChanged>(HandleWorkShopsChanged);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<WorkshopsSet>(HandleWorkShopsSet);
            messageBroker.Unsubscribe<NetworkWorkshopsSet>(HandleWorkShopsSet);

            messageBroker.Unsubscribe<WorkshopsChanged>(HandleWorkShopsChanged);
            messageBroker.Unsubscribe<NetworkWorkshopsChanged>(HandleWorkShopsChanged);
        }

        private void HandleWorkShopsSet(MessagePayload<WorkshopsSet> payload)
        {
            var data = payload.What;

            if (!TryGetId(data.Instance, out string townId)) return;
            network.SendAll(new NetworkWorkshopsSet(townId, data.Value.Length ));

            foreach (var item in data.Value)
            {
                if(item != null)
                {
                    // TODO: Optimize
                    if (!TryGetId(item, out string workShopId)) return;
                    network.SendAll(new NetworkWorkshopsChanged(townId, workShopId, data.Value.IndexOf(item)));
                }
            }
        }

        private void HandleWorkShopsSet(MessagePayload<NetworkWorkshopsSet> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetObject(data.TownId, out Town town)) return;

            town.Workshops = new Workshop[data.Length];
        }

        private void HandleWorkShopsChanged(MessagePayload<WorkshopsChanged> payload)
        {
            var data = payload.What;

            if (!TryGetId(data.Instance, out string townId)) return;
            if (!TryGetId(data.Value, out string workShopId)) return;
            network.SendAll(new NetworkWorkshopsChanged(townId, workShopId, data.Index));
        }

        private void HandleWorkShopsChanged(MessagePayload<NetworkWorkshopsChanged> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetObject(data.TownId, out Town town)) return;
            if (!objectManager.TryGetObject(data.WorkshopId, out Workshop workshop)) return;
            town.Workshops[data.Index] = workshop;
        }

        private bool TryGetId(object value, out string id)
        {
            id = null;
            if (value == null) return false;

            if (!objectManager.TryGetId(value, out id))
            {
                Logger.Error("Unable to get ID for instance of type {type}", value.GetType());
                return false;
            }
            return true;
        }
    }
}