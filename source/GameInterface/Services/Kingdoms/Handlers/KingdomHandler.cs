using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Armies.Patches;
using GameInterface.Services.Kingdoms.Messages;
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
            var data = payload.What.Data;

            if (objectManager.TryGetObject<Kingdom>(data.KingdomStringId, out var kingdom) == false)
            {
                Logger.Error("Unable to find Kingdom ({kingdomId})", data.KingdomStringId);
                return;
            }

            if (objectManager.TryGetObject<Hero>(data.LeaderHeroStringId, out var armyLeader) == false)
            {
                Logger.Error("Unable to find MobileParty ({armyLeaderId})", data.LeaderHeroStringId);
                return;
            }

            if (objectManager.TryGetObject<Settlement>(data.TargetSettlementStringId, out var targetSettlement) == false)
            {
                Logger.Error("Unable to find Settlement ({targetSettlement})", data.TargetSettlementStringId);
                return;
            }
            
            Army.ArmyTypes armyType = (Army.ArmyTypes)data.SelectedArmyType;

            ArmyCreationPatch.CreateArmyInKingdom(kingdom, armyLeader, targetSettlement, armyType, data.ArmyStringId);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<CreateArmyInKingdom>(HandleCreateArmyInKingdom);
        }
    }
}
