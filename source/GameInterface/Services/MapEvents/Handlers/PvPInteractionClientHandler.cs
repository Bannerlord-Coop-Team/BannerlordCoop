using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// Client side of the player-vs-player "hold on" popup. While another player (the attacker) drives a PvP interaction,
/// the defending player can only wait. The server broadcasts <see cref="NetworkPlayerInteractionStarted"/> /
/// <see cref="NetworkPlayerInteractionEnded"/>; this handler shows the blocking popup on the one client that controls
/// the defending party and closes it when the interaction ends or the battle starts. The server counterpart lives in
/// <see cref="ConversationRequestHandler"/>.
/// </summary>
internal class PvPInteractionClientHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PvPInteractionClientHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    // Party id the popup is currently shown for (always this client's own party), or null when nothing is shown.
    // Only touched on the game main thread.
    private string shownDefenderPartyId;
    private bool shownLocationInteraction;

    // This instance's player hero name, to tell instances apart in a combined log.
    private static string Who => Hero.MainHero?.Name?.ToString() ?? "?";

    public PvPInteractionClientHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<NetworkPlayerInteractionStarted>(Handle_NetworkPlayerInteractionStarted);
        messageBroker.Subscribe<NetworkPlayerInteractionEnded>(Handle_NetworkPlayerInteractionEnded);
        messageBroker.Subscribe<NetworkHidePvpPopup>(Handle_NetworkHidePvpPopup);
        messageBroker.Subscribe<NetworkClosePvpEncounter>(Handle_NetworkClosePvpEncounter);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPlayerInteractionStarted>(Handle_NetworkPlayerInteractionStarted);
        messageBroker.Unsubscribe<NetworkPlayerInteractionEnded>(Handle_NetworkPlayerInteractionEnded);
        messageBroker.Unsubscribe<NetworkHidePvpPopup>(Handle_NetworkHidePvpPopup);
        messageBroker.Unsubscribe<NetworkClosePvpEncounter>(Handle_NetworkClosePvpEncounter);

        // Make sure a popup never outlives the co-op session.
        if (shownDefenderPartyId != null)
        {
            HideWaitingPopup();
        }
    }

    /// <summary>An attacker opened a PvP interaction; show the popup if this client controls the defending party.</summary>
    private void Handle_NetworkPlayerInteractionStarted(MessagePayload<NetworkPlayerInteractionStarted> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;

        GameThread.Run(() =>
        {
            if (Campaign.Current == null) return;
            if (message.IsLocationInteraction && !LocationMissionTracker.IsLocationMission(Mission.Current)) return;

            // Already showing the popup for this party (a rate-limited retry re-broadcast): nothing to do.
            if (shownDefenderPartyId == message.DefenderPartyId &&
                shownLocationInteraction == message.IsLocationInteraction) return;

            if (shownDefenderPartyId != null)
                HideWaitingPopup();

            if (!objectManager.TryGetObject<PartyBase>(message.DefenderPartyId, out var defenderParty)) return;

            // Only the client that controls the defending party shows the popup.
            if (defenderParty.MobileParty?.IsControlledByThisInstance() != true) return;

            ShowWaitingPopup(message.AttackerName, message.IsLocationInteraction);
            shownDefenderPartyId = message.DefenderPartyId;
            shownLocationInteraction = message.IsLocationInteraction;

            if (!message.IsLocationInteraction)
            {
                // Tell the server which peer is the defender, so it can end the conversation and free the attacker if we
                // disconnect. On a client, SendAll targets the server.
                network.SendAll(new NetworkPvpDefenderShown(message.DefenderPartyId));
            }
        });
    }

    /// <summary>The attacker ended (or disconnected from) the interaction; close our popup and leave the encounter.</summary>
    private void Handle_NetworkPlayerInteractionEnded(MessagePayload<NetworkPlayerInteractionEnded> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;

        GameThread.Run(() =>
        {
            if (shownDefenderPartyId == null ||
                shownDefenderPartyId != message.DefenderPartyId ||
                shownLocationInteraction != message.IsLocationInteraction) return;

            HideWaitingPopup();

            if (!message.IsLocationInteraction)
            {
                // The attacker left without a battle. When the attacker engaged, native opened a local encounter menu
                // on the defender's client too (its own party ran HandleEncounterForMobileParty); close it so the
                // defender returns to the map instead of being stranded in the now-meaningless menu.
                CloseEncounter();
            }
        });
    }

    /// <summary>
    /// [Server -&gt; client] A PvP map event was finalized. If our party was one of the involved player parties, close
    /// our encounter menu. This is the reliable close for every involved party (attacker, defender, and joiners alike).
    /// </summary>
    private void Handle_NetworkClosePvpEncounter(MessagePayload<NetworkClosePvpEncounter> payload)
    {
        var message = payload.What;

        GameThread.RunSafe(() => ClosePvpEncounter(message), context: nameof(Handle_NetworkClosePvpEncounter));
    }

    private void ClosePvpEncounter(NetworkClosePvpEncounter message)
    {
        if (Campaign.Current == null)
        {
            return;
        }

        var partyIds = message.PartyIds;
        var matchedByPartyId = TryGetLocalParty(partyIds, out var localParty);
        var matchedByMapEvent = !matchedByPartyId && TryGetLocalPartyFromMapEvent(message, out localParty);
        var matchedBySurrender = !matchedByPartyId && !matchedByMapEvent && TryGetLocalPartyFromSurrender(message, out localParty);
        if (!matchedByPartyId && !matchedByMapEvent && !matchedBySurrender)
        {
            return;
        }
        var activeMission = MissionState.Current != null || Mission.Current != null;
        var localMapEvent = activeMission ? localParty?.MapEvent : null;
        ClearMapEventBackReferences(partyIds, localMapEvent != null ? localParty : null);

        Logger.Information("[PvPEncounterClose] Closing local encounter; partyIds=[{PartyIds}] surrenderedPartyId={SurrenderedPartyId} mapEventId={MapEventId}",
            FormatIds(partyIds),
            message.SurrenderedPartyId ?? "<none>",
            message.MapEventId ?? "<none>");

        BattleModeRegistry.End();

        if (shownDefenderPartyId != null)
            HideWaitingPopup();

        EndMissionIfItCanNoLongerResolve(message);

        CloseEncounter(localParty);
    }

    /// <summary>
    /// Ends a battle mission that nothing will ever end.
    /// </summary>
    /// <remarks>
    /// <see cref="CloseEncounter"/> deliberately leaves an active mission alone, because a battle that produced a
    /// RESULT still owns its screens - the winner reads the scoreboard and leaves when it chooses. A battle
    /// finalized with nothing decided is the opposite case: the only opposing player walked out of it, so the
    /// map event is gone and no end condition can ever fire. A defender cannot even reach one while still
    /// deploying, since vanilla's BattleEndLogic skips every check for as long as a BattleDeploymentHandler is
    /// on the mission - which is exactly how issue #1840 stranded a player in a PvP siege.
    ///
    /// A resolved MissionResult is the discriminator: the winner of a concluded battle always has one by the
    /// time the server finalizes, so its scoreboard is never touched here.
    /// </remarks>
    private void EndMissionIfItCanNoLongerResolve(NetworkClosePvpEncounter message)
    {
        var mission = Mission.Current;

        // The finalize destroys the map event before it sends this close, and both travel the same
        // reliable-ordered channel, so an unresolvable id here means the battle really has ended - not that
        // this client is merely behind.
        var battleStillExists = !string.IsNullOrEmpty(message.MapEventId)
            && objectManager.TryGetObject<MapEvent>(message.MapEventId, out _);

        if (!ShouldEndUnresolvableMission(
                hasMission: mission != null,
                hasMissionResult: mission?.MissionResult != null,
                missionEnded: mission?.MissionEnded == true,
                namesABattle: !string.IsNullOrEmpty(message.MapEventId),
                battleStillExists: battleStillExists))
        {
            return;
        }

        Logger.Information("[PvPEncounterClose] Battle {MapEventId} was finalized with nothing decided; ending the local mission that can no longer resolve",
            message.MapEventId);

        // Vanilla's own "leave this battle with no result" path: it fans OnRetreatMission out to the mission
        // logics before ending, which the deployment and spawn logics need, rather than cutting the mission off.
        mission.RetreatMission();
    }

    /// <summary>
    /// Pure rule for <see cref="EndMissionIfItCanNoLongerResolve"/>, as a seam so headless tests can cover it
    /// (no <see cref="Mission"/> exists in the test harness).
    /// </summary>
    /// <remarks>
    /// A resolved <c>MissionResult</c> is the discriminator between the two kinds of finalize. The winner of a
    /// concluded battle always has one by the time the server finalizes, so its scoreboard is never cut off; a
    /// battle abandoned by its only opponent never produces one, and that is the mission nothing else can end.
    /// The battle must also be verifiably GONE - a close that names no battle, or one this client can still
    /// resolve, is not evidence that the fight is over.
    /// </remarks>
    internal static bool ShouldEndUnresolvableMission(
        bool hasMission,
        bool hasMissionResult,
        bool missionEnded,
        bool namesABattle,
        bool battleStillExists)
    {
        if (!hasMission || hasMissionResult || missionEnded) return false;

        return namesABattle && !battleStillExists;
    }

    private void ClearMapEventBackReferences(string[] partyIds, PartyBase deferredParty = null)
    {
        foreach (var partyId in partyIds ?? Array.Empty<string>())
        {
            if (!objectManager.TryGetObject<PartyBase>(partyId, out var party))
                continue;

            if (party == deferredParty)
                continue;

            if (party.MapEventSide != null)
                party._mapEventSide = null;
        }
    }

    private bool TryGetLocalParty(string[] partyIds, out PartyBase party)
    {
        party = null;
        var ids = partyIds ?? Array.Empty<string>();

        if (objectManager.TryGetId(MobileParty.MainParty?.Party, out var localPartyId) &&
            Array.IndexOf(ids, localPartyId) >= 0)
        {
            party = MobileParty.MainParty.Party;
            return true;
        }

        if (TryGetCurrentEncounterParty(ids, out party))
            return true;

        foreach (var partyId in ids)
        {
            if (!objectManager.TryGetObject<PartyBase>(partyId, out var candidate))
                continue;

            if (candidate.MobileParty?.IsControlledByThisInstance() != true)
                continue;

            party = candidate;
            return true;
        }

        return false;
    }

    private bool TryGetCurrentEncounterParty(string[] partyIds, out PartyBase party)
    {
        party = null;

        var encounter = PlayerEncounter.Current;
        if (encounter == null)
            return false;

        var attacker = encounter._attackerParty;
        var defender = encounter._defenderParty;
        if (!ContainsPartyId(attacker, partyIds) && !ContainsPartyId(defender, partyIds))
            return false;

        if (IsLocalParty(attacker))
        {
            party = attacker;
            return true;
        }

        if (IsLocalParty(defender))
        {
            party = defender;
            return true;
        }

        return false;
    }

    private bool ContainsPartyId(PartyBase party, string[] partyIds)
    {
        foreach (var partyId in partyIds ?? Array.Empty<string>())
        {
            if (IsPartyId(party, partyId))
                return true;
        }

        return false;
    }

    private bool TryGetLocalPartyFromMapEvent(NetworkClosePvpEncounter message, out PartyBase party)
    {
        party = null;
        if (string.IsNullOrEmpty(message.MapEventId))
            return false;

        var mainParty = MobileParty.MainParty?.Party;
        if (IsMapEventId(MobileParty.MainParty?.MapEvent, message.MapEventId))
        {
            party = mainParty;
            return party != null;
        }

        var encounter = PlayerEncounter.Current;
        if (IsMapEventId(encounter?._mapEvent, message.MapEventId) ||
            IsMapEventId(GetPlayerEncounterBattle(), message.MapEventId) ||
            IsMapEventId(GetPlayerEncounterEncounteredBattle(), message.MapEventId))
        {
            if (IsLocalParty(encounter?._attackerParty))
                party = encounter._attackerParty;
            else if (IsLocalParty(encounter?._defenderParty))
                party = encounter._defenderParty;
            else
                party = mainParty;

            return party != null;
        }

        return false;
    }

    private bool TryGetLocalPartyFromSurrender(NetworkClosePvpEncounter message, out PartyBase party)
    {
        party = null;
        if (string.IsNullOrEmpty(message.SurrenderedPartyId))
            return false;

        var encounter = PlayerEncounter.Current;
        if (encounter == null)
            return false;

        var attacker = encounter._attackerParty;
        var defender = encounter._defenderParty;
        if (IsPartyId(attacker, message.SurrenderedPartyId))
            party = defender;
        else if (IsPartyId(defender, message.SurrenderedPartyId))
            party = attacker;
        else
            return false;

        return IsLocalParty(party);
    }

    private bool IsMapEventId(MapEvent mapEvent, string mapEventId)
    {
        if (mapEvent == null || string.IsNullOrEmpty(mapEventId))
            return false;

        return objectManager.TryGetId(mapEvent, out var resolvedMapEventId) && resolvedMapEventId == mapEventId;
    }

    private bool IsPartyId(PartyBase party, string partyId)
    {
        if (party == null || string.IsNullOrEmpty(partyId))
            return false;

        if (objectManager.TryGetId(party, out var resolvedPartyId) && resolvedPartyId == partyId)
            return true;

        return objectManager.TryGetObject<PartyBase>(partyId, out var resolvedParty) &&
               ReferenceEquals(resolvedParty, party);
    }

    private static bool IsLocalParty(PartyBase party)
    {
        if (party?.MobileParty == null)
            return false;

        return party.MobileParty == MobileParty.MainParty ||
               party.MobileParty.IsControlledByThisInstance();
    }

    private static string FormatIds(string[] ids)
        => ids == null || ids.Length == 0 ? "<none>" : string.Join(",", ids);

    private static MapEvent GetPlayerEncounterBattle()
    {
        try
        {
            return PlayerEncounter.Battle;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private static MapEvent GetPlayerEncounterEncounteredBattle()
    {
        try
        {
            return PlayerEncounter.EncounteredBattle;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    /// <summary>The defender was added to the map event (battle started); drop the popup — the battle menu blocks them now.</summary>
    private void Handle_NetworkHidePvpPopup(MessagePayload<NetworkHidePvpPopup> payload)
    {
        if (ModInformation.IsServer) return;

        var partyIds = payload.What.PartyIds;

        GameThread.Run(() =>
        {
            if (shownDefenderPartyId == null) return;
            if (shownLocationInteraction) return;
            if (Array.IndexOf(partyIds, shownDefenderPartyId) < 0) return;

            HideWaitingPopup();
        });
    }

    private static void ShowWaitingPopup(string attackerName, bool isLocationInteraction)
    {
        if (isLocationInteraction)
        {
            LocationPlayerInteractionWaitingOverlay.Instance.Show(attackerName);
            return;
        }

        var name = string.IsNullOrEmpty(attackerName) ? "Another player" : attackerName;

        // Button-less popup: the defender cannot dismiss it themselves; it is closed programmatically when the
        // interaction ends or the battle starts. Time stays server-authoritative, so the local game is not paused.
        InformationManager.ShowInquiry(new InquiryData(
            "Interaction",
            $"{name} is interacting with you, hold on...",
            false,
            false,
            string.Empty,
            string.Empty,
            null,
            null), false);
    }

    private void HideWaitingPopup()
    {
        if (shownLocationInteraction)
            LocationPlayerInteractionWaitingOverlay.Instance.Hide();
        else
            InformationManager.HideInquiry();

        shownDefenderPartyId = null;
        shownLocationInteraction = false;
    }

    /// <summary>
    /// Leaves the local encounter the party opened when the attacker engaged. Skipped only when a battle mission is
    /// actually running (the party belongs in it) — being on a map-event side is not enough, since a joiner sits on a
    /// side in the pre-battle encounter the attacker just abandoned. For such a joiner we clear its map-event back-ref
    /// so the dangling <see cref="MobileParty.MapEvent"/> stops the encounter menu reopening the instant
    /// <see cref="PlayerEncounter.Finish"/> closes it. Finish's postfix
    /// (<see cref="Patches.PlayerEncounterPatches"/>) holds the party so it does not immediately re-engage.
    /// </summary>
    private void CloseEncounter(PartyBase localParty = null)
    {
        var mainParty = localParty?.MobileParty ?? MobileParty.MainParty;
        Logger.Debug("[MapEvent] {Who}: CloseEncounter before: mission={Mission} encounter={Enc} mainPartyMapEvent={Me} menu={Menu}",
            Who,
            MissionState.Current != null,
            PlayerEncounter.Current != null,
            mainParty?.MapEvent != null,
            Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>");

        if (MissionState.Current != null || Mission.Current != null)
        {
            // Keep this party attached until vanilla processes its result screens after the player exits the mission.
            Logger.Information("[PvPEncounterClose] Leaving active mission open until the local player exits");
            return;
        }

        // A joiner stays bound to its map-event side after the attacker abandons the encounter: the abandon tears
        // the event down via FinalizeEvent, which (unlike FinishBattle) does not remove joined parties. The dangling
        // MainParty.MapEvent makes the encounter menu reopen the instant Finish closes it. Null the back-reference
        // directly to detach locally. NOT via the MapEventSide setter: that runs MapEventSide.RemovePartyInternal,
        // whose client patch calls FinalizeEvent when the removed party is its side's leader — which broadcasts a
        // finalize to the server (MapEventPatches.Prefix_FinalizeEventAux). We only want a local detach.
        if (mainParty?.MapEvent != null)
            mainParty.Party._mapEventSide = null;

        // The local player was captured in this battle: the captivity flow owns the UI (prisoner menu) and leaves the
        // encounter itself. Finishing/exiting here would close the capture screen. Mirrors
        // BattleHandler.Handle_NetworkMapEventFinalized.
        if (PlayerCaptivity.IsCaptive)
        {
            return;
        }

        // Leaving an encounter is handled by PlayerEncounter.Update, which is required to bring up loot screens and more
        // Do not finish the PlayerEncounter here
    }

    private static void ExitMapMenuMode(Campaign campaign, MapState mapState)
    {
        if (mapState?.AtMenu == true)
            mapState.ExitMenuMode();

        if (mapState != null)
            mapState.GameMenuId = null;
        if (campaign?.MapStateData != null)
            campaign.MapStateData.GameMenuId = null;
    }
}
