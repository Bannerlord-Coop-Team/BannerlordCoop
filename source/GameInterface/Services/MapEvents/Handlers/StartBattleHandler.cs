using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers
{
    public class StartBattleHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<StartBattleHandler>();
        private MobileParty lastAttackingParty;

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

            bool flag = false;
            if (defenderParty.CurrentSettlement != null)
            {
                if (defenderParty.MapEvent != null)
                {
                    flag = (defenderParty.MapEvent.MapEventSettlement == defenderParty.CurrentSettlement && (defenderParty.MapEvent.AttackerSide.LeaderParty.MapFaction == attackerParty.MapFaction || defenderParty.MapEvent.DefenderSide.LeaderParty.MapFaction == attackerParty.MapFaction));
                }
            }
            else
            {
                flag = (attackerParty != MobileParty.MainParty || !defenderParty.IsEngaging || defenderParty.ShortTermTargetParty != MobileParty.MainParty);
            }
            if (flag)
            {
                if (attackerParty == MobileParty.MainParty)
                {
                    MapState mapState = Game.Current.GameStateManager.ActiveState as MapState;
                    if (mapState != null)
                    {
                        mapState.OnMainPartyEncounter();
                    }
                }
            }

            if (lastAttackingParty == null || lastAttackingParty != attackerParty)
            {
                lastAttackingParty = attackerParty;
                Logger.Information(attackerParty.Name.ToString() + " attacks " + defenderParty.Name.ToString());
                EncounterManager.StartPartyEncounter(attackerParty.Party, defenderParty.Party);
            }            
        }
    }
}