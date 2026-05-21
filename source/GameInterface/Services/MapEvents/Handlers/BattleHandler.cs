using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers;

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

        messageBroker.Subscribe<PlayerLeaveBattle>(Handle_PlayerLeaveBattle);
        messageBroker.Subscribe<NetworkLeavePlayerBattle>(Handle_NetworkLeavePlayerBattle);

        messageBroker.Subscribe<PlayerSurrendered>(Handle_PlayerSurrendered);
        messageBroker.Subscribe<NetworkPlayerSurrendered>(Handle_NetworkPlayerSurrendered);

        messageBroker.Subscribe<PlayerEncounterStarted>(Handle_PlayerEncounterStarted);
        messageBroker.Subscribe<NetworkPlayerEncounterStarted>(Handle_NetworkPlayerEncounterStarted);

        messageBroker.Subscribe<MapEventInvolvedPartiesAdded>(Handle_MapEventInvolvedPartiesAdded);
        messageBroker.Subscribe<NetworkAddInvolvedParties>(Handle_NetworkAddInvolvedParties);
    }



    public void Dispose()
    {
        messageBroker.Unsubscribe<BattleStarted>(Handle_BattleStarted);
        messageBroker.Unsubscribe<NetworkStartBattle>(Handle_NetworkStartBattle);
        messageBroker.Unsubscribe<PlayerStartBattle>(Handle_PlayerStartBattle);
        messageBroker.Unsubscribe<NetworkStartPlayerBattle>(Handle_NetworkStartPlayerBattle);
        messageBroker.Unsubscribe<NetworkResponsePlayerBattle>(Handle_NetworkResponsePlayerBattle);

        messageBroker.Unsubscribe<PlayerLeaveBattle>(Handle_PlayerLeaveBattle);
        messageBroker.Unsubscribe<NetworkLeavePlayerBattle>(Handle_NetworkLeavePlayerBattle);
    }

    private void Handle_BattleStarted(MessagePayload<BattleStarted> payload)
    {
        var data = payload.What;

        if (!objectManager.TryGetIdWithLogging(data.Attacker, out var attackerPartyBaseId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Defender, out var defenderPartyBaseId)) return;

        var message = new NetworkStartBattle(attackerPartyBaseId, defenderPartyBaseId);

        network.SendAll(message);
    }

    private void Handle_NetworkStartBattle(MessagePayload<NetworkStartBattle> payload)
    {
        if (!objectManager.TryGetObjectWithLogging(payload.What.AttackerId, out PartyBase attacker)) return;
        if (!objectManager.TryGetObjectWithLogging(payload.What.DefenderId, out PartyBase defender)) return;

        EncounterManagerPatches.OverrideOnPartyInteraction(attacker, defender);
    }

    private void Handle_PlayerStartBattle(MessagePayload<PlayerStartBattle> payload)
    {
        if (!objectManager.TryGetIdWithLogging(MobileParty.MainParty, out var playerPartyId)) return;
        
        var message = new NetworkStartPlayerBattle(playerPartyId);

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

    private void Handle_PlayerLeaveBattle(MessagePayload<PlayerLeaveBattle> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out string mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.MobileParty, out string mobilePartyId)) return;

        network.SendAll(new NetworkLeavePlayerBattle(mobilePartyId, mapEventId));

        using (new AllowedThread())
        {
            payload.What.MapEvent.FinalizeEvent();

            GameMenu.ExitToLast();

            Campaign.Current.PlayerEncounter = null;
            Campaign.Current.LocationEncounter = null;
        }
    }

    private void Handle_NetworkLeavePlayerBattle(MessagePayload<NetworkLeavePlayerBattle> payload)
    {
        if (!objectManager.TryGetObjectWithLogging(payload.What.MapEventId, out MapEvent mapEvent)) return;
        if (!objectManager.TryGetObjectWithLogging(payload.What.MobilePartyId, out MobileParty playerParty)) return;

        Settlement currentSettlement = playerParty.CurrentSettlement;
        int numberOfInvolvedMen = mapEvent.GetNumberOfInvolvedMen(playerParty.Party.Side);
        bool forcePlayerOutFromSettlement;
        if (currentSettlement?.SiegeEvent != null)
        {
            forcePlayerOutFromSettlement = currentSettlement?.MapFaction != playerParty.MapFaction;
        }
        else
        {
            forcePlayerOutFromSettlement = true;
        }

        playerParty.TeleportPartyToOutSideOfEncounterRadius();

        var enemyParties = mapEvent._sides.Single(side => !side.Parties.Any(x => x.Party == playerParty.Party));

        foreach (var mapEventParty in enemyParties.Parties)
        {
            var disableTime = DefaultMobilePartyAIModelPatches.DisablePlayerAttackTimes.GetOrCreateValue(mapEventParty.Party.MobileParty.Ai);

            disableTime[playerParty] = CampaignTime.HoursFromNow(2);
        }

        mapEvent.FinalizeEvent();

        if (playerParty.CurrentSettlement != null && playerParty.AttachedTo == null && forcePlayerOutFromSettlement)
        {
            LeaveSettlementAction.ApplyForParty(playerParty);
        }

        if (playerParty.BesiegerCamp != null)
        {
            playerParty.BesiegerCamp = null;
        }
        if (mapEvent != null && !mapEvent.IsFinalized && !mapEvent.IsRaid && numberOfInvolvedMen == playerParty.Party.NumberOfHealthyMembers)
        {
            //MapEvent mapEvent2 = mapEvent;
            //PlayerEncounter playerEncounter = PlayerEncounter.Current;
            //FlattenedTroopRoster[] priorTroops;
            //if (playerEncounter == null)
            //{
            //    priorTroops = null;
            //}
            //else
            //{
            //    BattleSimulation battleSimulation = mapEvent.BattleSimulation;
            //    priorTroops = ((battleSimulation != null) ? battleSimulation.SelectedTroops : null);
            //}

            // TODO sync simulation
            //mapEvent2.SimulateBattleSetup(priorTroops);
            //mapEvent.SimulateBattleRound((playerParty.Party.Side == BattleSideEnum.Attacker) ? 1 : 0, (playerParty.Party.Side == BattleSideEnum.Attacker) ? 0 : 1);
        }
        if (currentSettlement != null)
        {
            EncounterManager.StartSettlementEncounter(playerParty, currentSettlement);
        }
    }

    private void Handle_PlayerSurrendered(MessagePayload<PlayerSurrendered> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out string mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.MobileParty, out string mobilePartyId)) return;

        var message = new NetworkPlayerSurrendered(mobilePartyId, mapEventId);

        network.SendAll(message);

        using (new AllowedThread())
        {
            PlayerEncounter.Current._playerSurrender = true;
            payload.What.MobileParty.BesiegerCamp = null;

            GameMenu.ActivateGameMenu("taken_prisoner");

            PlayerEncounter.Current._stateHandled = true;
        }
    }

    private void Handle_NetworkPlayerSurrendered(MessagePayload<NetworkPlayerSurrendered> payload)
    {
        if (!objectManager.TryGetObjectWithLogging(payload.What.MapEventId, out MapEvent mapEvent)) return;
        if (!objectManager.TryGetObjectWithLogging(payload.What.MobilePartyId, out MobileParty mobileParty)) return;

        mapEvent.DoSurrender(mobileParty.Party.Side);
        mapEvent.FinalizeEvent();
    }

    private void Handle_PlayerEncounterStarted(MessagePayload<PlayerEncounterStarted> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out string mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.MobileParty, out string MobilePartyId)) return;

        var message = new NetworkPlayerEncounterStarted(mapEventId, MobilePartyId);

        network.SendAll(message);
    }

    private void Handle_NetworkPlayerEncounterStarted(MessagePayload<NetworkPlayerEncounterStarted> payload)
    {
        if (!objectManager.TryGetObjectWithLogging(payload.What.MapEventId, out MapEvent mapEvent)) return;
        if (!objectManager.TryGetObjectWithLogging(payload.What.MobilePartyId, out MobileParty mobileParty)) return;

        if (mobileParty != MobileParty.MainParty) return;

        PlayerEncounter.Current._mapEvent = mapEvent;
    }

    private void Handle_MapEventInvolvedPartiesAdded(MessagePayload<MapEventInvolvedPartiesAdded> payload)
    {
        var message = payload.What;
        if (!objectManager.TryGetIdWithLogging(message.MapEvent, out var mapEventSideId))
            return;

        var partyIds = new List<string>();

        foreach (var addedParty in message.AddedParties)
        {
            if (objectManager.TryGetIdWithLogging(addedParty, out var mapEventPartyId))
            {
                partyIds.Add(mapEventPartyId);
            }
        }

        network.SendAll(new NetworkAddInvolvedParties(
            mapEventSideId,
            partyIds.ToArray()
        ));
    }

    private void Handle_NetworkAddInvolvedParties(MessagePayload<NetworkAddInvolvedParties> payload)
    {
        var message = payload.What;

        if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.MapEventId, out var mapEvent))
            return;

        using (new AllowedThread())
        {
            foreach(var mapEventPartyId in message.MapEventPartyIds)
            {
                if (!objectManager.TryGetObjectWithLogging<MapEventParty>(mapEventPartyId, out var mapEventParty))
                    continue;

                mapEvent.TroopUpgradeTracker.AddParty(mapEventParty);
            }
        }
    }
}