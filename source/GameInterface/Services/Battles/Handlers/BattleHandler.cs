using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Battles.Messages;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Battles.Handlers
{
    internal class BattleHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        private static readonly ILogger Logger = LogManager.GetLogger<BattleHandler>();

        public BattleHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<BattleStarted>(Handle);
            messageBroker.Subscribe<NetworkStartBattle>(Handle);
            messageBroker.Subscribe<PlayerStartBattle>(Handle);
            messageBroker.Subscribe<NetworkStartPlayerBattle>(Handle);
            messageBroker.Subscribe<NetworkResponsePlayerBattle>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<BattleStarted>(Handle);
            messageBroker.Unsubscribe<NetworkStartBattle>(Handle);
            messageBroker.Unsubscribe<PlayerStartBattle>(Handle);
            messageBroker.Unsubscribe<NetworkStartPlayerBattle>(Handle);
            messageBroker.Unsubscribe<NetworkResponsePlayerBattle>(Handle);
        }

        private void Handle(MessagePayload<BattleStarted> payload)
        {
            var data = payload.What;

            if(data.Defender.IsSettlement)
            {
                network.SendAll(new NetworkStartBattle(data.Attacker.StringId, data.Defender.Settlement.StringId, true));
            }
            else
            {
                network.SendAll(new NetworkStartBattle(data.Attacker.StringId, data.Defender.MobileParty.StringId, false));
            }

            
        }

        private void Handle(MessagePayload<NetworkStartBattle> payload)
        {
            if(payload.What.IsSettlement)
            {
                objectManager.TryGetObject(payload.What.AttackerId, out MobileParty attacker);
                objectManager.TryGetObject(payload.What.DefenderId, out Settlement defender);
                EncounterManagerPatches.OverrideOnPartyInteraction(attacker, defender.Party);
            }
            else
            {
                objectManager.TryGetObject(payload.What.AttackerId, out MobileParty attacker);
                objectManager.TryGetObject(payload.What.DefenderId, out MobileParty defender);
                EncounterManagerPatches.OverrideOnPartyInteraction(attacker, defender.Party);
            }
        }

        private void Handle(MessagePayload<PlayerStartBattle> payload)
        {
            var message = new NetworkStartPlayerBattle(MobileParty.MainParty.StringId);

            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkStartPlayerBattle> payload)
        {
            var obj = payload.What;

            objectManager.TryGetObject(obj.PlayerPartyId, out MobileParty playerParty);

            var message = new NetworkResponsePlayerBattle(playerParty.MapEvent.StringId);

            network.Send(payload.Who as NetPeer, message);
        }

        private void Handle(MessagePayload<NetworkResponsePlayerBattle> payload)
        {
            var obj = payload.What;

            objectManager.TryGetObject(obj.MapEventString, out MapEvent mapEvent);

            MapEventSide playerSide = null;

            //if (mapEvent.AttackerSide.LeaderParty == PartyBase.MainParty)
            //{
            //    playerSide = mapEvent.AttackerSide;
            //}
            //else if (mapEvent.DefenderSide.LeaderParty == PartyBase.MainParty)
            //{
            //    playerSide = mapEvent.DefenderSide;
            //}
            //else
            //{
            //    Logger.Error("Player is not a leader party, expected error. Needs to be handled eventually" +
            //        "Loop through sides and find which contains player party");
            //}

            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    //Campaign.Current.PlayerEncounter._mapEvent = mapEvent;

                    //Get which side player is on
                    //MobileParty.MainParty.Party._mapEventSide = playerSide;

                    //EncounterGameMenuBehavior menu = Campaign.Current.CampaignBehaviorManager.GetBehavior<EncounterGameMenuBehavior>();
                    Campaign.Current.PlayerEncounter.JoinBattleInternal(TaleWorlds.Core.BattleSideEnum.Attacker);
                    //menu.game_menu_encounter_leave_on_consequence(default);

                    Logger.Information(Campaign.Current.MapEventManager._mapEvents.ToString());
                }
            });
        }
    }
}