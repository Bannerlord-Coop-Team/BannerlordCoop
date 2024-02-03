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
using TaleWorlds.CampaignSystem;
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
            messageBroker.Subscribe<RemoveMobilePartyInArmy>(HandleChangeRemoveMobilePartyInArmy);
            messageBroker.Subscribe<DisbandArmy>(HandleChangeDisbandArmy);
        }


        private void HandleChangeDisbandArmy(MessagePayload<DisbandArmy> payload)
        {
            var obj = payload.What;


            IArmyRegistry armyRegistry = new ArmyRegistry();
            armyRegistry.TryGetValue(obj.ArmyId, out Army army);
            
            if (armyRegistry != null)
            {
                Logger.Error("Unable to find Army ({armyId})", obj.ArmyId);
                return;
            }
            Army.ArmyDispersionReason armyReason = (Army.ArmyDispersionReason)Army.ArmyDispersionReason.Parse(typeof(Army.ArmyDispersionReason), obj.Reason);
            DisbandArmyPatch.DisbandArmy(army, armyReason);
        }


        private void HandleChangeRemoveMobilePartyInArmy(MessagePayload<RemoveMobilePartyInArmy> payload)
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

            //TODO: Wait for Amry creation / deletion sync add, cannot call the ArmyPach because army will be null

            //ArmyPatches.RemoveMobilePartyInArmy(mobileParty, leaderMobileParty.Army);

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

            //TODO: Wait for Amry creation / deletion sync add, cannot call the ArmyPach because army will be null

            //ArmyPatches.AddMobilePartyInArmy(mobileParty, leaderMobileParty.Army);
              
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<AddMobilePartyInArmy>(HandleChangeAddMobilePartyInArmy);
            messageBroker.Unsubscribe<RemoveMobilePartyInArmy>(HandleChangeRemoveMobilePartyInArmy);
            messageBroker.Unsubscribe<DisbandArmy>(HandleChangeDisbandArmy);
        }

    }
}
