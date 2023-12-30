using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers
{
    public class EndBattleHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<EndBattleHandler>();

        public EndBattleHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<EndBattle>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<EndBattle>(Handle);
        }
            
        private void Handle(MessagePayload<EndBattle> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject<MobileParty>(payload.partyId, out var party) == false)
            {
                Logger.Error("Unable to find attacking MobileParty ({attackerPartyId})", payload.partyId);
                return;
            }
            MapEventUpdatePatch.RunOriginalFinishBattle(party.MapEvent);
        }
    }
}