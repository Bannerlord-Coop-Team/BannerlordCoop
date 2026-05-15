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
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Battles.Handlers;

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
        messageBroker.Subscribe<BattleStarted>(Handle_BattleStarted);
        messageBroker.Subscribe<NetworkStartBattle>(Handle_NetworkStartBattle);
        messageBroker.Subscribe<PlayerStartBattle>(Handle_PlayerStartBattle);
        messageBroker.Subscribe<NetworkStartPlayerBattle>(Handle_NetworkStartPlayerBattle);
        messageBroker.Subscribe<NetworkResponsePlayerBattle>(Handle_NetworkResponsePlayerBattle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BattleStarted>(Handle_BattleStarted);
        messageBroker.Unsubscribe<NetworkStartBattle>(Handle_NetworkStartBattle);
        messageBroker.Unsubscribe<PlayerStartBattle>(Handle_PlayerStartBattle);
        messageBroker.Unsubscribe<NetworkStartPlayerBattle>(Handle_NetworkStartPlayerBattle);
        messageBroker.Unsubscribe<NetworkResponsePlayerBattle>(Handle_NetworkResponsePlayerBattle);
    }

    private void Handle_BattleStarted(MessagePayload<BattleStarted> payload)
    {
        var data = payload.What;

        if (!objectManager.TryGetId(data.Attacker, out var partyId))
        {
            return;
        }

        if(data.Defender.IsSettlement)
        {
            network.SendAll(new NetworkStartBattle(partyId, data.Defender.Settlement.StringId, true));
        }
        else
        {
            network.SendAll(new NetworkStartBattle(partyId, data.Defender.MobileParty.StringId, false));
        }
    }

    private void Handle_NetworkStartBattle(MessagePayload<NetworkStartBattle> payload)
    {
        if (!objectManager.TryGetObject(payload.What.AttackerId, out PartyBase attacker)) {
            Logger.Error("Failed to get {var} with id: {id}", nameof(PartyBase), payload.What.AttackerId);
            return;
        }

        if (!objectManager.TryGetObject(payload.What.DefenderId, out PartyBase defender))
        {
            Logger.Error("Failed to get {var} with id: {id}", nameof(PartyBase), payload.What.DefenderId);
            return;
        }

        EncounterManagerPatches.OverrideOnPartyInteraction(attacker, defender);
    }

    private void Handle_PlayerStartBattle(MessagePayload<PlayerStartBattle> payload)
    {
        var message = new NetworkStartPlayerBattle(MobileParty.MainParty.StringId);

        network.SendAll(message);
    }

    private void Handle_NetworkStartPlayerBattle(MessagePayload<NetworkStartPlayerBattle> payload)
    {
        var obj = payload.What;

        objectManager.TryGetObject(obj.PlayerPartyId, out MobileParty playerParty);

        var message = new NetworkResponsePlayerBattle(playerParty.MapEvent.StringId);

        network.Send(payload.Who as NetPeer, message);
    }

    private void Handle_NetworkResponsePlayerBattle(MessagePayload<NetworkResponsePlayerBattle> payload)
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