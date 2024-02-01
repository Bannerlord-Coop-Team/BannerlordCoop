using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.Fiefs.Handlers;
using GameInterface.Services.Fiefs.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Handlers
{
    public class ArmyHandler : IHandler
    {
        
        private static readonly ILogger Logger = LogManager.GetLogger<ArmyHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        public ArmyHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;

            messageBroker.Subscribe<AddMobilePartyInArmy>(HandleChangeAddMobilePartyInArmy);

        }

        //Generate Handler Methods
        private void HandleChangeAddMobilePartyInArmy(MessagePayload<AddMobilePartyInArmy> payload)
        {
            var obj = payload.What;

            if (objectManager.TryGetObject(obj.MobilePartyId, out MobileParty mobileParty) == false)
            {
                Logger.Error("Unable to find MobileParty ({mobilePartyId})", obj.MobilePartyId);
                return;
            }
        
            if (objectManager.TryGetObject(obj.LeaderMobilePartyId, out MobileParty leaderMobileParty) == false)
            {
                Logger.Error("Unable to find MobileParty ({leaderMobilePartyId})", obj.LeaderMobilePartyId);
                return;
            }

                
            ArmyPatches.AddMobilePartyInArmy(mobileParty, leaderMobileParty.Army);
              
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<AddMobilePartyInArmy>(HandleChangeAddMobilePartyInArmy);
        }

    }
}
