using System;
using System.Collections.Generic;
using System.Text;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.StanceLinks.Messages;
using GameInterface.Services.StanceLinks.Messages.Data;
using GameInterface.Services.Stances.Data;
using GameInterface.Services.Stances.Messages.Lifetime;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.StanceLinks.Handlers
{
    /// <summary>
    /// Handler for <see cref="StanceLink"/> messages
    /// </summary>
    internal class StanceLinkDataHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<StanceLinkDataHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        public StanceLinkDataHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;

            //subscribe to internal and network messages
            messageBroker.Subscribe<StanceLinkFactionChanged>(Handle_StanceLinkFactionChanged);
            messageBroker.Subscribe<NetworkStanceLinkFactionChanged>(Handle_NetworkStanceLinkFactionChanged);
        }

        public void Dispose()
        {
            //unsubscribe to internal and network messages
            messageBroker.Unsubscribe<StanceLinkFactionChanged>(Handle_StanceLinkFactionChanged);
            messageBroker.Unsubscribe<NetworkStanceLinkFactionChanged>(Handle_NetworkStanceLinkFactionChanged);
        }

        private void Handle_StanceLinkFactionChanged(MessagePayload<StanceLinkFactionChanged> payload)
        {
            //create temp objects to manipulate data easier
            var stanceLink = payload.What.StanceLink;
            var faction = payload.What.Faction;
            var isFaction1 = payload.What.IsFaction1;

            //get ID of necessary object - if error abort
            if (objectManager.TryGetId(stanceLink, out var stanceLinkId) == false) return; 
            if (objectManager.TryGetId(faction, out var factionId) == false) return;

            //send network message to update object on client side
            var data = new StanceLinkFactionChangedData(stanceLinkId, factionId, isFaction1);
            var message = new NetworkStanceLinkFactionChanged(data);
            network.SendAll(message);
        }


        private void Handle_NetworkStanceLinkFactionChanged(MessagePayload<NetworkStanceLinkFactionChanged> obj)
        {
            var payload = obj.What.Data;

            IFaction faction;

            if (objectManager.TryGetObject(payload.FactionId, out Kingdom kingdom) == false)
            {
                if (objectManager.TryGetObject(payload.FactionId, out Clan clan) == false)
                {
                    Logger.Error("Failed to get faction, {id}", payload.FactionId);
                    return;
                }
                else
                {
                    faction = clan;
                }
            }
            else
            {
                faction = kingdom;
            }

            if (objectManager.TryGetObject(payload.StanceId, out StanceLink stance) == false)
            {
                Logger.Error("Failed to get StanceLink, {id}", payload.StanceId);
                return;
            }


            using (new AllowedThread())
            {
                if(payload.IsFaction1)
                {
                    stance.Faction1 = faction;
                }
                else
                {
                    stance.Faction2 = faction;
                }
            }
        }
    }
}
