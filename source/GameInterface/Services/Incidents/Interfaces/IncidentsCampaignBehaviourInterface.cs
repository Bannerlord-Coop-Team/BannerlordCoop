using Common.Logging;
using Common.Network;
using GameInterface.Extentions;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Incidents.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.Missions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Incidents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using IncidentTrigger = TaleWorlds.CampaignSystem.CampaignBehaviors.IncidentsCampaignBehaviour.IncidentTrigger;

namespace GameInterface.Services.Incidents.Interfaces;

public interface IIncidentsCampaignBehaviourInterface : IGameAbstraction
{
    void OnHourlyTick();
    void OnGameMenuOptionSelected(Player player, MobileParty mainParty, Hero mainHero, string gameMenuId, string optionId);
    void OnGameMenuOpened(Player player, MobileParty mainParty, Hero mainHero, string menuContextId);
    void OnMapEventEnded(MapEvent mapEvent);
    void ConversationEnded(Player player, MobileParty mainParty, Hero mainHero);
    void OnHeirSelectionOver(string playerHeroId);
    void OnSettlementEntered(MobileParty mobileParty);
    void OnSettlementLeft(MobileParty mobileParty);
    void OnIncidentResolved(string playerHeroId, string incidentId, CampaignTime incidentCooldown);
    void AddPlayerKeys(string playerHeroId);
    void TryInvokeIncident(Player player, MobileParty playerParty, Hero playerHero, IncidentsCampaignBehaviour.IncidentTrigger trigger);
    void ResendPendingIncident(Player player);
    List<Incident> GetDebugInvokableIncidents(Player player, MobileParty playerParty, Hero playerHero);
    bool TryDebugInvokeIncident(Player player, MobileParty playerParty, Hero playerHero, Incident incident, out string error);
}

public class IncidentsCampaignBehaviourInterface : IIncidentsCampaignBehaviourInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<IncidentsCampaignBehaviourInterface>();

    private const IncidentTrigger EnteringSettlementTriggers = IncidentTrigger.EnteringCastle | IncidentTrigger.EnteringTown | IncidentTrigger.EnteringVillage;
    private const IncidentTrigger LeavingSettlementTriggers = IncidentTrigger.LeavingTown | IncidentTrigger.LeavingVillage | IncidentTrigger.LeavingSettlement;

    private readonly IPlayerManager playerManager;
    private readonly ISessionIncidentsPlayerDataInterface sessionIncidentsPlayerDataInterface;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMissionMembershipRegistry missionMembershipRegistry;

    private readonly HashSet<string> heldBattleRollHeroIds = new();

    public IncidentsCampaignBehaviourInterface(
        IPlayerManager playerManager,
        ISessionIncidentsPlayerDataInterface sessionIncidentsPlayerDataInterface,
        IObjectManager objectManager,
        INetwork network,
        IMissionMembershipRegistry missionMembershipRegistry = null)
    {
        this.playerManager = playerManager;
        this.sessionIncidentsPlayerDataInterface = sessionIncidentsPlayerDataInterface;
        this.objectManager = objectManager;
        this.network = network;
        this.missionMembershipRegistry = missionMembershipRegistry;
    }

    public void OnHourlyTick()
    {
        foreach (var playerParty in Campaign.Current.CampaignObjectManager.GetPlayerMobileParties())
        {
            var partyLeader = playerParty.LeaderHero;
            if (partyLeader == null) continue;

            if (!playerManager.TryGetPlayer(playerParty, out var player)) continue;

            // Roll for players whose battle ended while they were still in its mission once they've left it
            if (heldBattleRollHeroIds.Contains(player.HeroId) && !IsInMission(player))
            {
                heldBattleRollHeroIds.Remove(player.HeroId);
                TryInvokeIncident(player, playerParty, partyLeader, IncidentTrigger.LeavingBattle);
                continue;
            }

            float randomChance = playerParty.RandomFloatWithSeed((uint)CampaignTime.Now.NumTicks);
            if (playerParty.SiegeEvent != null && randomChance < Campaign.Current.Models.IncidentModel.GetIncidentTriggerProbabilityDuringSiege())
            {
                TryInvokeIncident(player, playerParty, partyLeader, IncidentTrigger.DuringSiege);
                continue;
            }

            // Vanilla's WaitingInSettlement trigger is skipped because DisableWaitInSettlementOption removes the settlement wait menus
        }
    }

    public void OnGameMenuOptionSelected(Player player, MobileParty mainParty, Hero mainHero, string gameMenuId, string optionId)
    {
        if (mainParty.RandomFloatWithSeed((uint)CampaignTime.Now.NumTicks) < Campaign.Current.Models.IncidentModel.GetIncidentTriggerGlobalProbability())
        {
            if (gameMenuId == "town" && optionId == "town_leave")
            {
                TryInvokeIncident(player, mainParty, mainHero, IncidentTrigger.LeavingTown | IncidentTrigger.LeavingSettlement);
            }
            if (gameMenuId == "castle" && optionId == "leave")
            {
                TryInvokeIncident(player, mainParty, mainHero, IncidentTrigger.LeavingCastle | IncidentTrigger.LeavingSettlement);
            }
            if (gameMenuId == "village" && optionId == "leave")
            {
                TryInvokeIncident(player, mainParty, mainHero, IncidentTrigger.LeavingVillage | IncidentTrigger.LeavingSettlement);
            }
        }
    }

    public void OnGameMenuOpened(Player player, MobileParty mainParty, Hero mainHero, string menuContextId)
    {
        if (!sessionIncidentsPlayerDataInterface.CanInvokeSettlementEvent(player.HeroId)) return;

        if (mainParty.RandomFloatWithSeed((uint)CampaignTime.Now.NumTicks) < Campaign.Current.Models.IncidentModel.GetIncidentTriggerGlobalProbability())
        {
            if (menuContextId == "town")
            {
                TryInvokeIncident(player, mainParty, mainHero, IncidentTrigger.EnteringTown);
                sessionIncidentsPlayerDataInterface.SetCanInvokeSettlementEvent(player.HeroId, false);
            }
            if (menuContextId == "village")
            {
                TryInvokeIncident(player, mainParty, mainHero, IncidentTrigger.EnteringVillage);
                sessionIncidentsPlayerDataInterface.SetCanInvokeSettlementEvent(player.HeroId, false);
            }
            if (menuContextId == "castle")
            {
                TryInvokeIncident(player, mainParty, mainHero, IncidentTrigger.EnteringCastle);
                sessionIncidentsPlayerDataInterface.SetCanInvokeSettlementEvent(player.HeroId, false);
            }
        }
    }

    public void OnMapEventEnded(MapEvent mapEvent)
    {
        if (!mapEvent.ContainsPlayerParty() || mapEvent.IsNavalMapEvent) return;

        // Vanilla only rolls for a winning player, so only the winning side's player parties are checked below
        if (!mapEvent.HasWinner) return;

        if ((mapEvent.IsFieldBattle || mapEvent.IsHideoutBattle) && !mapEvent.AttackerSide.IsSurrendered && !mapEvent.DefenderSide.IsSurrendered)
        {
            foreach (var playerParty in mapEvent.GetPlayerPartiesOnSide(mapEvent.WinningSide))
            {
                var party = playerParty.Party?.MobileParty;
                if (party == null) continue;

                var partyLeader = party.LeaderHero;
                if (partyLeader == null) continue;

                if (!playerManager.TryGetPlayer(party, out var player)) continue;

                if (party.RandomFloatWithSeed((uint)CampaignTime.Now.NumTicks) >= Campaign.Current.Models.IncidentModel.GetIncidentTriggerGlobalProbability()) continue;

                TryInvokeIncident(player, party, partyLeader, IncidentTrigger.LeavingBattle);
            }
        }
    }

    public void ConversationEnded(Player player, MobileParty mainParty, Hero mainHero)
    {
        if (mainParty.CurrentSettlement == null && mainParty.RandomFloatWithSeed((uint)CampaignTime.Now.NumTicks) < Campaign.Current.Models.IncidentModel.GetIncidentTriggerGlobalProbability())
        {
            TryInvokeIncident(player, mainParty, mainHero, IncidentTrigger.LeavingEncounter);
        }
    }

    public void OnHeirSelectionOver(string playerHeroId)
    {
        sessionIncidentsPlayerDataInterface.SetGlobalCooldown(playerHeroId, CampaignTime.Now + CampaignTime.Hours(1f));
        sessionIncidentsPlayerDataInterface.ClearIncidentCooldowns(playerHeroId);
    }

    public void OnSettlementEntered(MobileParty mobileParty)
    {
        if (!playerManager.TryGetPlayer(mobileParty, out var player)) return;

        sessionIncidentsPlayerDataInterface.SetCanInvokeSettlementEvent(player.HeroId, true);
    }

    public void OnSettlementLeft(MobileParty mobileParty)
    {
        if (!playerManager.TryGetPlayer(mobileParty, out var player)) return;

        sessionIncidentsPlayerDataInterface.SetCanInvokeSettlementEvent(player.HeroId, false);
    }

    public void OnIncidentResolved(string playerHeroId, string incidentId, CampaignTime incidentCooldown)
    {
        if (!TryGetIncidentsBehavior(out var incidentsBehavior)) return;

        sessionIncidentsPlayerDataInterface.AddIncidentOnCooldown(playerHeroId, incidentId, CampaignTime.Now + incidentCooldown);
        sessionIncidentsPlayerDataInterface.SetGlobalCooldown(playerHeroId, CampaignTime.Now + incidentsBehavior.GetCooldownTime());
    }

    // Matches vanilla OnNewGameCreated, so a new character can't get an incident straight away
    public void AddPlayerKeys(string playerHeroId)
    {
        if (!TryGetIncidentsBehavior(out var incidentsBehavior)) return;

        sessionIncidentsPlayerDataInterface.AddPlayerKeys(playerHeroId, CampaignTime.Now + incidentsBehavior.GetCooldownTime());
    }

    public void TryInvokeIncident(Player player, MobileParty playerParty, Hero playerHero, IncidentTrigger trigger)
    {
        // Hold the roll for a battle that ends before its player has left the mission
        if (trigger == IncidentTrigger.LeavingBattle && IsInMission(player))
        {
            heldBattleRollHeroIds.Add(player.HeroId);
            return;
        }

        if (!IsPlayerAvailableForIncident(player, playerParty, playerHero)) return;

        // Skip if a pending incident exists
        if (sessionIncidentsPlayerDataInterface.TryGetPendingIncident(player.HeroId, out _)) return;

        // Run vanilla checks for invoking incidents
        bool isEnteringSettlement = (trigger & EnteringSettlementTriggers) != 0;
        if (isEnteringSettlement && (playerParty.CurrentSettlement == null || playerParty.CurrentSettlement.IsSettlementBusy(this))) return;

        bool isLeavingSettlement = (trigger & LeavingSettlementTriggers) != 0;
        if (isLeavingSettlement && playerParty.LastVisitedSettlement.IsSettlementBusy(this)) return;

        if (sessionIncidentsPlayerDataInterface.IsOnGlobalCooldown(player.HeroId)) return;

        if (!TryGetIncidentsBehavior(out var incidentsBehavior)) return;

        sessionIncidentsPlayerDataInterface.CheckIncidentsOnCooldown(player.HeroId);

        RollActiveIncidentSeed(player.HeroId, incidentsBehavior);

        var occurableEventsForTrigger = new List<Incident>();
        using (new MainHeroSubstitutionScope(playerHero, playerParty))
        {
             occurableEventsForTrigger = GetOccurableEventsForTrigger(player.HeroId, trigger);
        }
        if (occurableEventsForTrigger.Count > 0)
        {
            Incident randomElement = occurableEventsForTrigger.GetRandomElement();
            InvokeIncident(player, randomElement);
        }
    }

    // A player who disconnects with an incident open is sent it again on rejoin, so leaving can't skip its outcome
    public void ResendPendingIncident(Player player)
    {
        if (!sessionIncidentsPlayerDataInterface.TryGetPendingIncident(player.HeroId, out var incidentId)) return;
        if (!playerManager.TryGetPeer(player.ControllerId, out var peer)) return;

        // Nothing rolls while an incident is pending, so the stored seed is still the one it was rolled with
        network.Send(peer, new NetworkIncidentInvoked(incidentId, sessionIncidentsPlayerDataInterface.GetActiveIncidentSeed(player.HeroId)));
    }

    public List<Incident> GetDebugInvokableIncidents(Player player, MobileParty playerParty, Hero playerHero)
    {
        List<Incident> incidents = new();
        if (!TryGetIncidentsBehavior(out var incidentsBehavior)) return incidents;

        // Only the behaviour's seed, so listing doesn't replace the stored seed of a pending incident
        incidentsBehavior._activeIncidentSeed = CampaignTime.Now.NumTicks;

        using (new MainHeroSubstitutionScope(playerHero, playerParty))
        {
            foreach (Incident incident in MBObjectManager.Instance.GetObjectTypeList<Incident>())
            {
                // Every incident is checked regardless of trigger, and some vanilla conditions assume their trigger's state,
                // e.g. incident_love_marriage reads LastVisitedSettlement without a null check
                try
                {
                    if (incident.CanIncidentBeInvoked()) incidents.Add(incident);
                }
                catch (Exception e)
                {
                    Logger.Debug(e, "Skipping {IncidentId} because its conditions threw for this player", incident.StringId);
                }
            }
        }

        return incidents;
    }

    // Skips the trigger, probability and cooldowns, but keeps the availability guards and the incident's own conditions
    public bool TryDebugInvokeIncident(Player player, MobileParty playerParty, Hero playerHero, Incident incident, out string error)
    {
        error = null;

        if (!IsPlayerAvailableForIncident(player, playerParty, playerHero))
        {
            error = "Player is disconnected, dead, a prisoner, in a battle or in a mission";
            return false;
        }

        if (sessionIncidentsPlayerDataInterface.TryGetPendingIncident(player.HeroId, out var pendingIncidentId))
        {
            error = $"Player still has {pendingIncidentId} pending. Answer it or run coop.debug.incident.clear_cooldowns";
            return false;
        }

        if (!TryGetIncidentsBehavior(out var incidentsBehavior))
        {
            error = "IncidentsCampaignBehaviour is unavailable";
            return false;
        }

        RollActiveIncidentSeed(player.HeroId, incidentsBehavior);

        bool canBeInvoked;
        using (new MainHeroSubstitutionScope(playerHero, playerParty))
        {
            canBeInvoked = incident.CanIncidentBeInvoked();
        }

        if (!canBeInvoked)
        {
            error = $"{incident.StringId} conditions don't hold for this player";
            return false;
        }

        InvokeIncident(player, incident);
        return true;
    }

    // Incidents only show on the world map, so skip players who are disconnected, dead, a prisoner, in a battle or in a mission.
    // Vanilla's conversation check is done by the client in IncidentsHandler, since the server never has one.
    private bool IsPlayerAvailableForIncident(Player player, MobileParty playerParty, Hero playerHero)
    {
        if (playerManager.IsOwnerOfHeroDisconnected(playerHero)) return false;
        if (!playerHero.IsAlive || playerHero.IsPrisoner) return false;
        if (playerParty.MapEvent?.IsFinalized == false) return false;
        if (IsInMission(player)) return false;

        return true;
    }

    private bool IsInMission(Player player)
    {
        return missionMembershipRegistry?.IsControllerInMission(player.ControllerId) == true;
    }

    // Vanilla conditions and effects pick their targets with the behaviour's seed, so evaluate them with this player's seed
    private void RollActiveIncidentSeed(string playerHeroId, IncidentsCampaignBehaviour incidentsBehavior)
    {
        long activeIncidentSeed = CampaignTime.Now.NumTicks;
        sessionIncidentsPlayerDataInterface.SetActiveIncidentSeed(playerHeroId, activeIncidentSeed);
        incidentsBehavior._activeIncidentSeed = activeIncidentSeed;
    }

    private List<Incident> GetOccurableEventsForTrigger(string playerHeroId, IncidentTrigger trigger)
    {
        List<Incident> list = new();
        foreach (Incident incident in MBObjectManager.Instance.GetObjectTypeList<Incident>())
        {
            if (!objectManager.TryGetIdWithLogging(incident, out var incidentId)) continue;
            if (sessionIncidentsPlayerDataInterface.IsIncidentOnCooldown(playerHeroId, incidentId)) continue;

            if ((incident.Trigger & trigger) != 0 && incident.CanIncidentBeInvoked())
            {
                list.Add(incident);
            }
        }
        return list;
    }

    private void InvokeIncident(Player player, Incident incident)
    {
        if (!objectManager.TryGetIdWithLogging(incident, out var incidentId)) return;
        if (!playerManager.TryGetPeer(player.ControllerId, out var peer)) return;

        // IncidentsHandler only applies an option for the incident the player was last sent
        sessionIncidentsPlayerDataInterface.SetPendingIncident(player.HeroId, incidentId);

        // The client fills in the incident's text with the same seed the server rolled it with
        var message = new NetworkIncidentInvoked(incidentId, sessionIncidentsPlayerDataInterface.GetActiveIncidentSeed(player.HeroId));
        network.Send(peer, message);
    }

    private bool TryGetIncidentsBehavior(out IncidentsCampaignBehaviour incidentsCampaignBehavior)
    {
        incidentsCampaignBehavior = Campaign.Current?.GetCampaignBehavior<IncidentsCampaignBehaviour>();
        if (incidentsCampaignBehavior != null) return true;

        Logger.Debug("Skipping incidents campaign behavior update because the campaign behavior is unavailable.");
        return false;
    }
}