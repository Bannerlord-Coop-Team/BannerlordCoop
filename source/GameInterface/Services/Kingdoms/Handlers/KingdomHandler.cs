using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Towns.Handlers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Kingdoms.Handlers
{
    public class KingdomHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<KingdomHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        public KingdomHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;

            messageBroker.Subscribe<CreateArmyInKingdom>(HandleCreateArmyInKingdom);
        }


        private void HandleCreateArmyInKingdom(MessagePayload<CreateArmyInKingdom> payload)
        {
            var obj = payload.What;

            Kingdom kingdom = Campaign.Current.CampaignObjectManager.Kingdoms.Find(k => k.StringId == obj.KingdomId);
            if (kingdom == null)
            {
                Logger.Error("Unable to find Kingdom ({kingdomId})", obj.KingdomId);
                return;
            }

            if (objectManager.TryGetObject(obj.ArmyLeaderId, out Hero armyLeader) == false)
            {
                Logger.Error("Unable to find MobileParty ({armyLeaderId})", obj.ArmyLeaderId);
                return;
            }

            if (objectManager.TryGetObject(obj.TargetSettlement, out Settlement targetSettlement) == false)
            {
                Logger.Error("Unable to find Settlement ({targetSettlement})", obj.TargetSettlement);
                return;
            }
            
            Army.ArmyTypes arselectedArmyTypeyType = (Army.ArmyTypes)Army.ArmyTypes.Parse(typeof(Army.ArmyTypes), obj.SelectedArmyType);
            
            KingdomPatches.CreateArmyInKingdom(kingdom, armyLeader, targetSettlement, arselectedArmyTypeyType);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<CreateArmyInKingdom>(HandleCreateArmyInKingdom);
        }
    }
}
