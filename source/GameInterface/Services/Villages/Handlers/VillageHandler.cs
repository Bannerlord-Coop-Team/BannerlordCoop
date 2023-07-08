using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Messages;
using GameInterface.Services.Villages.Patches;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Settlements.Village;

namespace GameInterface.Services.Villages.Handlers
{
    /// <summary>
    /// Handles changes to parties for settlement entry and exit.
    /// </summary>
    public class VillageHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<VillageHandler>();

        public VillageHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<VillageStateChanged>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<VillageStateChanged>(Handle);
        }

        private void Handle(MessagePayload<VillageStateChanged> obj)
        {
            if (objectManager.TryGetObject(obj.What.PartyId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Could not handle VillageStateChanged, PartyId not found: {id}", obj.What.PartyId);
                return;
            }

            Village village = Village.All.Find(x => x.StringId == obj.What.PartyId);

            ChangeVillageStatePatch.RunOriginalApplyInternal(village, (VillageStates)obj.What.NewState, mobileParty);
        }
    }
}
