using System;
using System.Collections.Generic;
using System.Text;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Handlers;
using GameInterface.Services.Heroes.Messages.Collections;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Handlers
{
    internal class TroopRosterCollectionHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<TroopRosterCollectionHandler>();

        public TroopRosterCollectionHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network, ILogger logger)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;

            messageBroker.Subscribe<TroopRosterDataUpdated>(Handle);
            messageBroker.Subscribe<NetworkUpdateTroopRosterData>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<TroopRosterDataUpdated>(Handle);
            messageBroker.Unsubscribe<NetworkUpdateTroopRosterData>(Handle);
        }

        private void Handle(MessagePayload<TroopRosterDataUpdated> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetId(data.Instance, out string RosterId)) return;

            objectManager.TryGetId(data.Value.Character, out string CharacterId);

            network.SendAll(new NetworkUpdateTroopRosterData(RosterId, CharacterId, 
                data.Value.DeltaXp,
                data.Value._number,
                data.Value._woundedNumber,
                data.Value._xp,
                data.Index));
        }
        private void Handle(MessagePayload<NetworkUpdateTroopRosterData> payload)
        {
            var data = payload.What;

            if (!objectManager.TryGetObject(data.RosterId, out TroopRoster troopRoster)) return;

            objectManager.TryGetObject(data.CharacterId, out CharacterObject character);

            TroopRosterElement newElement = new TroopRosterElement()
            {
                Character = character,
                DeltaXp = data.DeltaXp,
                Number = data.Number,
                WoundedNumber = data.WoundedNumber,
                Xp = data.Xp,
            };

            troopRoster.data[data.Index] = newElement;
            troopRoster.UpdateVersion();
            troopRoster.ValidateTroopListCache();
        }
    }
}