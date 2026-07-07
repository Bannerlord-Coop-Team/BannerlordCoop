using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
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

    // Set when a server close (NetworkClosePvpEncounter) arrives while a battle mission is still running. The
    // close cannot happen mid-mission, so it is retried on the next CampaignTick once the mission has ended
    // (Handle_CampaignTick). Static because CloseEncounter is static.
    private static bool pendingEncounterClose;

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
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPlayerInteractionStarted>(Handle_NetworkPlayerInteractionStarted);
        messageBroker.Unsubscribe<NetworkPlayerInteractionEnded>(Handle_NetworkPlayerInteractionEnded);
        messageBroker.Unsubscribe<NetworkHidePvpPopup>(Handle_NetworkHidePvpPopup);
        messageBroker.Unsubscribe<NetworkClosePvpEncounter>(Handle_NetworkClosePvpEncounter);
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);

        // Make sure a popup never outlives the co-op session.
        if (shownDefenderPartyId != null)
        {
            InformationManager.HideInquiry();
            shownDefenderPartyId = null;
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

            // Already showing the popup for this party (a rate-limited retry re-broadcast): nothing to do.
            if (shownDefenderPartyId == message.DefenderPartyId) return;

            if (!objectManager.TryGetObject<PartyBase>(message.DefenderPartyId, out var defenderParty)) return;

            // Only the client that controls the defending party shows the popup.
            if (defenderParty.MobileParty?.IsControlledByThisInstance() != true) return;

            ShowWaitingPopup(message.AttackerName);
            shownDefenderPartyId = message.DefenderPartyId;

            // Tell the server which peer is the defender, so it can end the conversation and free the attacker if we
            // disconnect. On a client, SendAll targets the server.
            network.SendAll(new NetworkPvpDefenderShown(message.DefenderPartyId));
        });
    }

    /// <summary>The attacker ended (or disconnected from) the interaction; close our popup and leave the encounter.</summary>
    private void Handle_NetworkPlayerInteractionEnded(MessagePayload<NetworkPlayerInteractionEnded> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;

        GameThread.Run(() =>
        {
            if (shownDefenderPartyId == null || shownDefenderPartyId != message.DefenderPartyId) return;

            HideWaitingPopup();

            // The attacker left without a battle. When the attacker engaged, native opened a local encounter menu
            // on the defender's client too (its own party ran HandleEncounterForMobileParty); close it so the
            // defender returns to the map instead of being stranded in the now-meaningless menu.
            CloseEncounter();
        });
    }

    /// <summary>
    /// [Server -&gt; client] A PvP map event was finalized. If our party was one of the involved player parties, close
    /// our encounter menu. This is the reliable close for every involved party (attacker, defender, and joiners alike).
    /// </summary>
    private void Handle_NetworkClosePvpEncounter(MessagePayload<NetworkClosePvpEncounter> payload)
    {
        var partyIds = payload.What.PartyIds;

        GameThread.Run(() =>
        {
            if (Campaign.Current == null) return;

            if (!TryGetLocalParty(partyIds, out var localParty)) return;
            ClearMapEventBackReferences(partyIds);

            Logger.Debug("[MapEvent] {Who}: NetworkClosePvpEncounter for my party; closing", Who);

            BattleModeRegistry.End();

            if (shownDefenderPartyId != null)
                HideWaitingPopup();

            CloseEncounter(localParty);
        });
    }

    private void ClearMapEventBackReferences(string[] partyIds)
    {
        foreach (var partyId in partyIds ?? Array.Empty<string>())
        {
            if (!objectManager.TryGetObject<PartyBase>(partyId, out var party))
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
        if (party == null || partyIds == null || partyIds.Length == 0)
            return false;

        return objectManager.TryGetId(party, out var partyId) &&
               Array.IndexOf(partyIds, partyId) >= 0;
    }

    private static bool IsLocalParty(PartyBase party)
    {
        if (party?.MobileParty == null)
            return false;

        return party.MobileParty == MobileParty.MainParty ||
               party.MobileParty.IsControlledByThisInstance();
    }

    /// <summary>
    /// Retries a deferred encounter close. <see cref="CloseEncounter"/> defers when the server's close arrives
    /// while the battle mission is still running; once the mission has torn down this runs the close so the
    /// player is not left stranded on the post-battle encounter menu.
    /// </summary>
    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (ModInformation.IsServer) return;
        if (!pendingEncounterClose) return;
        if (MissionState.Current != null) return;

        pendingEncounterClose = false;
        Logger.Debug("[MapEvent] {Who}: running deferred encounter close (mission ended)", Who);
        CloseEncounter();
    }

    /// <summary>The defender was added to the map event (battle started); drop the popup — the battle menu blocks them now.</summary>
    private void Handle_NetworkHidePvpPopup(MessagePayload<NetworkHidePvpPopup> payload)
    {
        if (ModInformation.IsServer) return;

        var partyIds = payload.What.PartyIds;

        GameThread.Run(() =>
        {
            if (shownDefenderPartyId == null) return;
            if (Array.IndexOf(partyIds, shownDefenderPartyId) < 0) return;

            HideWaitingPopup();
        });
    }

    private static void ShowWaitingPopup(string attackerName)
    {
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
        InformationManager.HideInquiry();
        shownDefenderPartyId = null;
    }

    /// <summary>
    /// Leaves the local encounter the party opened when the attacker engaged. Skipped only when a battle mission is
    /// actually running (the party belongs in it) — being on a map-event side is not enough, since a joiner sits on a
    /// side in the pre-battle encounter the attacker just abandoned. For such a joiner we clear its map-event back-ref
    /// so the dangling <see cref="MobileParty.MapEvent"/> stops the encounter menu reopening the instant
    /// <see cref="PlayerEncounter.Finish"/> closes it. Finish's postfix
    /// (<see cref="Patches.PlayerEncounterPatches"/>) holds the party so it does not immediately re-engage.
    /// </summary>
    private static void CloseEncounter(PartyBase localParty = null)
    {
        var mainParty = localParty?.MobileParty ?? MobileParty.MainParty;
        Logger.Debug("[MapEvent] {Who}: CloseEncounter before: mission={Mission} encounter={Enc} mainPartyMapEvent={Me} menu={Menu}",
            Who,
            MissionState.Current != null,
            PlayerEncounter.Current != null,
            mainParty?.MapEvent != null,
            Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>");

        // A battle mission is still running — the party belongs in it; never pull it out now. The server's
        // NetworkClosePvpEncounter arrives before the local mission tears down, so defer the close and let
        // Handle_CampaignTick retry it once the mission has ended.
        if (MissionState.Current != null)
        {
            pendingEncounterClose = true;
            Logger.Debug("[MapEvent] {Who}: CloseEncounter deferred (mission still running)", Who);
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
        if (PlayerCaptivity.IsCaptive) return;

        if (PlayerEncounter.Current != null)
            PlayerEncounter.Finish(true);

        // Finishing the encounter does not itself close the open game menu; exit it explicitly. Mirrors
        // BattleHandler.Handle_NetworkMapEventFinalized, the proven post-battle menu teardown.
        GameMenu.ExitToLast();

        Logger.Debug("[MapEvent] {Who}: CloseEncounter after: encounter={Enc} mainPartyMapEvent={Me} menu={Menu}",
            Who,
            PlayerEncounter.Current != null,
            mainParty?.MapEvent != null,
            Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>");
    }
}
