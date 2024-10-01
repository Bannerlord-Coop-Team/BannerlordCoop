using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers
{
    /// <summary>
    /// Game Interface handler for Starting Map Events
    /// </summary>
    public class StartBattleHandler : IHandler
    {
        private readonly ILogger Logger = LogManager.GetLogger<StartBattleHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        public StartBattleHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<StartBattle>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<StartBattle>(Handle);
        }

        private void Handle(MessagePayload<StartBattle> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject<MobileParty>(payload.attackerPartyId, out var attackerParty) == false)
            {
                Logger.Error("Unable to find attacking MobileParty ({attackerPartyId})", payload.attackerPartyId);
                return;
            }
            if (objectManager.TryGetObject<MobileParty>(payload.defenderPartyId, out var defenderParty) == false)
            {
                Logger.Error("Unable to find defending MobileParty ({defenderPartyId})", payload.defenderPartyId);
                return;
            }

            StartBattleActionPatch.OverrideOnPartyInteraction(defenderParty, attackerParty);
        }
    }
}