using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Reflection;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Common.Extensions;
using Common.Util;

namespace GameInterface.Services.MapEvents.Handlers
{
    /// <summary>
    /// Game Interface handler for Ending Map Events
    /// </summary>
    public class EndBattleHandler : IHandler
    {
        private readonly ILogger Logger = LogManager.GetLogger<EndBattleHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

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

            if (party.MapEvent == null)
            {
                Logger.Error("Party ({partyName}) has no MapEvent but tried to end a MapEvent", party.Name.ToString());
                return;
            }

            MapEventUpdatePatch.OverrideFinishBattle(party.MapEvent);
        }
    }
}