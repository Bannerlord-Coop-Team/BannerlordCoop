using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Equipments.Messages.Events;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Equipments.Messages;
using Serilog;
using TaleWorlds.Core;
using System;
using GameInterface.Services.Equipments.Data;
using HarmonyLib;
using System.Reflection;
using System.Diagnostics;
using GameInterface.Services.Heroes.Messages.Collections;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;


namespace GameInterface.Services.Heroes.Handlers
{
    /// <summary>
    /// Handles all changes to Equipments on client.
    /// </summary>
    public class HeroCollectionHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<HeroCollectionHandler>();

        public HeroCollectionHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<VolunteerTypesArrayUpdated>(Handle);
            messageBroker.Subscribe<NetworkUpdateArray>(Handle);

            messageBroker.Subscribe<ChildrenListUpdated>(Handle);
            messageBroker.Subscribe<NetworkUpdateChildrenList>(Handle);

            messageBroker.Subscribe<CaravanListUpdated>(Handle);
            messageBroker.Subscribe<NetworkUpdateCaravanList>(Handle);
            messageBroker.Subscribe<CaravanListRemoved>(Handle);
            messageBroker.Subscribe<NetworkRemoveCaravanList>(Handle);

            messageBroker.Subscribe<AlleyListUpdated>(Handle);
            messageBroker.Subscribe<NetworkUpdateAlleyList>(Handle);
            messageBroker.Subscribe<AlleyListRemoved>(Handle);
            messageBroker.Subscribe<NetworkRemoveAlleyList>(Handle);

            messageBroker.Subscribe<WorkshopListUpdated>(Handle);
            messageBroker.Subscribe<NetworkUpdateWorkshopList>(Handle);
            messageBroker.Subscribe<WorkshopListRemoved>(Handle);
            messageBroker.Subscribe<NetworkRemoveWorkshopList>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<VolunteerTypesArrayUpdated>(Handle);
            messageBroker.Unsubscribe<NetworkUpdateArray>(Handle);

            messageBroker.Unsubscribe<ChildrenListUpdated>(Handle);
            messageBroker.Unsubscribe<NetworkUpdateChildrenList>(Handle);

            messageBroker.Unsubscribe<CaravanListUpdated>(Handle);
            messageBroker.Unsubscribe<NetworkUpdateCaravanList>(Handle);
            messageBroker.Unsubscribe<CaravanListRemoved>(Handle);
            messageBroker.Unsubscribe<NetworkRemoveCaravanList>(Handle);

            messageBroker.Unsubscribe<AlleyListUpdated>(Handle);
            messageBroker.Unsubscribe<NetworkUpdateAlleyList>(Handle);
            messageBroker.Unsubscribe<AlleyListRemoved>(Handle);
            messageBroker.Unsubscribe<NetworkRemoveAlleyList>(Handle);

            messageBroker.Unsubscribe<WorkshopListUpdated>(Handle);
            messageBroker.Unsubscribe<NetworkUpdateWorkshopList>(Handle);
            messageBroker.Unsubscribe<WorkshopListRemoved>(Handle);
            messageBroker.Unsubscribe<NetworkRemoveWorkshopList>(Handle);
        }

        private void Handle(MessagePayload<VolunteerTypesArrayUpdated> payload)
        {
            var data = payload.What;

            if (!TryGetId(data.Instance, out string HeroId)) return;
            if (!TryGetId(data.Value, out string CharacterObjectId)) return;

            network.SendAll(new NetworkUpdateArray(HeroId, CharacterObjectId, data.Index));
        }

        private void Handle(MessagePayload<NetworkUpdateArray> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetObject(data.HeroId, out Hero hero)) return;
            if (!objectManager.TryGetObject(data.ValueId, out CharacterObject characterObject)) return;

            hero.VolunteerTypes[data.Index] = characterObject;
        }

        private void Handle(MessagePayload<ChildrenListUpdated> payload)
        {
            var data = payload.What;

            if (!TryGetId(data.Instance, out string HeroId)) return;
            if (!TryGetId(data.Value, out string ChildId)) return;

            network.SendAll(new NetworkUpdateChildrenList(HeroId, ChildId));
        }

        private void Handle(MessagePayload<NetworkUpdateChildrenList> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetObject(data.HeroId, out Hero hero)) return;
            if (!objectManager.TryGetObject(data.ValueId, out Hero child)) return;

            hero._children.Add(child);
        }

        private void Handle(MessagePayload<CaravanListUpdated> payload)
        {
            var data = payload.What;

            if (!TryGetId(data.Instance, out string HeroId)) return;
            if (!TryGetId(data.Value, out string CaravanId)) return;

            network.SendAll(new NetworkUpdateCaravanList(HeroId, CaravanId));
        }

        private void Handle(MessagePayload<NetworkUpdateCaravanList> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetObject(data.HeroId, out Hero hero)) return;
            if (!objectManager.TryGetObject(data.ValueId, out CaravanPartyComponent caravan)) return;

            hero.OwnedCaravans.Add(caravan);
        }

        private void Handle(MessagePayload<CaravanListRemoved> payload)
        {
            var data = payload.What;

            if (!TryGetId(data.Instance, out string HeroId)) return;
            if (!TryGetId(data.Value, out string CaravanId)) return;

            network.SendAll(new NetworkRemoveCaravanList(HeroId, CaravanId));
        }

        private void Handle(MessagePayload<NetworkRemoveCaravanList> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetObject(data.HeroId, out Hero hero)) return;
            if (!objectManager.TryGetObject(data.ValueId, out CaravanPartyComponent caravan)) return;

            hero.OwnedCaravans.Remove(caravan);
        }

        private void Handle(MessagePayload<AlleyListUpdated> payload)
        {
            var data = payload.What;

            if (!TryGetId(data.Instance, out string HeroId)) return;
            if (!TryGetId(data.Value, out string AlleyId)) return;

            network.SendAll(new NetworkUpdateAlleyList(HeroId, AlleyId));
        }

        private void Handle(MessagePayload<NetworkUpdateAlleyList> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetObject(data.HeroId, out Hero hero)) return;
            if (!objectManager.TryGetObject(data.ValueId, out Alley alley)) return;

            hero.OwnedAlleys.Add(alley);
        }

        private void Handle(MessagePayload<AlleyListRemoved> payload)
        {
            var data = payload.What;

            if (!TryGetId(data.Instance, out string HeroId)) return;
            if (!TryGetId(data.Value, out string AlleyId)) return;

            network.SendAll(new NetworkRemoveAlleyList(HeroId, AlleyId));
        }

        private void Handle(MessagePayload<NetworkRemoveAlleyList> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetObject(data.HeroId, out Hero hero)) return;
            if (!objectManager.TryGetObject(data.ValueId, out Alley alley)) return;

            hero.OwnedAlleys.Remove(alley);
        }

        private void Handle(MessagePayload<WorkshopListUpdated> payload)
        {
            var data = payload.What;

            if (!TryGetId(data.Instance, out string HeroId)) return;
            if (!TryGetId(data.Value, out string WorkshopId)) return;

            network.SendAll(new NetworkUpdateWorkshopList(HeroId, WorkshopId));
        }

        private void Handle(MessagePayload<NetworkUpdateWorkshopList> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetObject(data.HeroId, out Hero hero)) return;
            if (!objectManager.TryGetObject(data.ValueId, out Workshop workshop)) return;

            hero._ownedWorkshops.Add(workshop);
        }

        private void Handle(MessagePayload<WorkshopListRemoved> payload)
        {
            var data = payload.What;

            if (!TryGetId(data.Instance, out string HeroId)) return;
            if (!TryGetId(data.Value, out string WorkshopId)) return;

            network.SendAll(new NetworkRemoveWorkshopList(HeroId, WorkshopId));
        }

        private void Handle(MessagePayload<NetworkRemoveWorkshopList> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetObject(data.HeroId, out Hero hero)) return;
            if (!objectManager.TryGetObject(data.ValueId, out Workshop workshop)) return;

            hero._ownedWorkshops.Remove(workshop);  
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

