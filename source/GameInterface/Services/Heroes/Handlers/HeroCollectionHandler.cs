using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Messages.Collections;
using GameInterface.Services.ObjectManager;
using static GameInterface.Services.ObjectManager.ObjectManager;
using Serilog;
using System;
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
            messageBroker.Subscribe<ChildrenListUpdated>(Handle);
            messageBroker.Subscribe<NetworkUpdateChildrenList>(Handle);

            messageBroker.Subscribe<CaravanListUpdated>(Handle);
            messageBroker.Subscribe<NetworkUpdateCaravanList>(Handle);
            messageBroker.Subscribe<CaravanListRemoved>(Handle);
            messageBroker.Subscribe<NetworkRemoveCaravanList>(Handle);

            messageBroker.Subscribe<WorkshopListUpdated>(Handle);
            messageBroker.Subscribe<NetworkUpdateWorkshopList>(Handle);
            messageBroker.Subscribe<WorkshopListRemoved>(Handle);
            messageBroker.Subscribe<NetworkRemoveWorkshopList>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ChildrenListUpdated>(Handle);
            messageBroker.Unsubscribe<NetworkUpdateChildrenList>(Handle);

            messageBroker.Unsubscribe<CaravanListUpdated>(Handle);
            messageBroker.Unsubscribe<NetworkUpdateCaravanList>(Handle);
            messageBroker.Unsubscribe<CaravanListRemoved>(Handle);
            messageBroker.Unsubscribe<NetworkRemoveCaravanList>(Handle);

            messageBroker.Unsubscribe<WorkshopListUpdated>(Handle);
            messageBroker.Unsubscribe<NetworkUpdateWorkshopList>(Handle);
            messageBroker.Unsubscribe<WorkshopListRemoved>(Handle);
            messageBroker.Unsubscribe<NetworkRemoveWorkshopList>(Handle);
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

            GameThread.Run(() =>
            {
                try
                {
                    if (!objectManager.TryGetObject(data.HeroId, out Hero hero)) return;
                    if (!objectManager.TryGetObject(data.ValueId, out Hero child)) return;

                    using (new AllowedThread())
                    {
                        hero._children.Add(child);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to apply NetworkUpdateChildrenList");
                }
            });
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

            GameThread.Run(() =>
            {
                try
                {
                    if (!objectManager.TryGetObject(data.HeroId, out Hero hero)) return;
                    if (!objectManager.TryGetObject(data.ValueId, out PartyComponent caravan)) return;

                    using (new AllowedThread())
                    {
                        hero.OwnedCaravans.Add((CaravanPartyComponent)caravan);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to apply NetworkUpdateCaravanList");
                }
            });
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

            GameThread.Run(() =>
            {
                try
                {
                    if (!objectManager.TryGetObject(data.HeroId, out Hero hero)) return;
                    if (!objectManager.TryGetObject(data.ValueId, out PartyComponent caravan)) return;

                    using (new AllowedThread())
                    {
                        hero.OwnedCaravans.Remove((CaravanPartyComponent)caravan);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to apply NetworkRemoveCaravanList");
                }
            });
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

            GameThread.Run(() =>
            {
                try
                {
                    if (!objectManager.TryGetObject(data.HeroId, out Hero hero)) return;
                    if (!objectManager.TryGetObject(data.ValueId, out Workshop workshop)) return;

                    using (new AllowedThread())
                    {
                        if (!hero._ownedWorkshops.Contains(workshop))
                        {
                            hero._ownedWorkshops.Add(workshop);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to apply NetworkUpdateWorkshopList");
                }
            });
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

            GameThread.Run(() =>
            {
                try
                {
                    if (!objectManager.TryGetObject(data.HeroId, out Hero hero)) return;
                    if (!objectManager.TryGetObject(data.ValueId, out Workshop workshop)) return;

                    using (new AllowedThread())
                    {
                        hero._ownedWorkshops.Remove(workshop);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to apply NetworkRemoveWorkshopList");
                }
            });
        }

        private bool TryGetId<T>(T value, out string id)
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

