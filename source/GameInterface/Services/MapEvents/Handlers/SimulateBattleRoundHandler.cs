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
    /// <summary>
    /// Game Interface handler for Starting Map Events
    /// </summary>
    public class SimulateBattleRoundHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<SimulateBattleRoundHandler>();

        public SimulateBattleRoundHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<SimulateBattleRound>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SimulateBattleRound>(Handle);
        }

        private void Handle(MessagePayload<SimulateBattleRound> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject<MobileParty>(payload.PartyId, out var attackerParty) == false)
            {
                Logger.Error("Unable to find attacking MobileParty ({attackerPartyId})", payload.PartyId);
                return;
            }

            MapEvent mapEvent = attackerParty.MapEvent;

            SimulateBattlePatch.OverrideSimulateBattleRound(mapEvent, (BattleSideEnum)payload.Side, payload.Advantage);
        }
    }
}