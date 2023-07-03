using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using GameInterface.Services.MapEvents.Patches;

namespace Coop.Core.Server.Services.MapEvents
{
    /// <summary>
    /// Handles changes to parties for settlement entry and exit.
    /// </summary>
    public class SettlementExitEnterHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public SettlementExitEnterHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<SettlementEnterRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SettlementEnterRequest>(Handle);
        }

        private void Handle(MessagePayload<SettlementEnterRequest> obj)
        {
            MobileParty mobileParty = null;
            MBGUID guid = new MBGUID((uint)int.Parse(obj.What.PartyId));
            foreach (MobileParty party in MobileParty.All)
            {
                if (party.Id == guid)
                {
                    mobileParty = party;
                }

            }

            Settlement settlement = Settlement.Find(obj.What.StringId);

            EncounterManagerPatches.RunOriginalStartSettlementEncounter(mobileParty, settlement);

            network.SendAll(new SettlementEnterAllowed(obj.What.StringId, obj.What.PartyId));
        }
    }
}
