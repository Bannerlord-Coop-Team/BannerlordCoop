using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ItemRosters.Handlers;
using GameInterface.Services.ItemRosters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Towns.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Handlers
{
    internal class TownLifetimeHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<TownLifetimeHandler>();

        public TownLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;

            messageBroker.Subscribe<TownCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateTown>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<TownCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateTown>(Handle);
        }

        private void Handle(MessagePayload<TownCreated> payload)
        {
            if (objectManager.AddNewObject(payload.What.Town, out string newId) == false)
            {
                Logger.Error("Failed to add {type} to manager", typeof(Town));
                return;
            }

            network.SendAll(new NetworkCreateTown(newId));
        }

        private void Handle(MessagePayload<NetworkCreateTown> payload)
        {
            var newTown = ObjectHelper.SkipConstructor<Town>();

            var data = payload.What;

            if (objectManager.AddExisting(data.TownId, newTown) == false)
            {
                Logger.Error("Failed to add {type} to manager with id {id}", typeof(Town), data.TownId);
                return;
            }
        }
    }
}
