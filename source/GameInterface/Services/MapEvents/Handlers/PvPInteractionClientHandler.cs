using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using System;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using Serilog;
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

    // Party id the popup is currently shown for (always this client's own party), or null when nothing is shown.
    // Only touched on the game main thread.
    private string shownDefenderPartyId;

    // This instance's player hero name, to tell instances apart in a combined log.
    private static string Who => Hero.MainHero?.Name?.ToString() ?? "?";

    public PvPInteractionClientHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;

        messageBroker.Subscribe<NetworkPlayerInteractionStarted>(Handle_NetworkPlayerInteractionStarted);
        messageBroker.Subscribe<NetworkPlayerInteractionEnded>(Handle_NetworkPlayerInteractionEnded);
        messageBroker.Subscribe<MapEventInvolvedPartiesAdded>(Handle_MapEventInvolvedPartiesAdded);
        messageBroker.Subscribe<NetworkClosePvpEncounter>(Handle_NetworkClosePvpEncounter);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkPlayerInteractionStarted>(Handle_NetworkPlayerInteractionStarted);
        messageBroker.Unsubscribe<NetworkPlayerInteractionEnded>(Handle_NetworkPlayerInteractionEnded);
        messageBroker.Unsubscribe<MapEventInvolvedPartiesAdded>(Handle_MapEventInvolvedPartiesAdded);
        messageBroker.Unsubscribe<NetworkClosePvpEncounter>(Handle_NetworkClosePvpEncounter);

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
        if (ModInformation.IsServer) return;

        var partyIds = payload.What.PartyIds;

        GameThread.Run(() =>
        {
            if (Campaign.Current == null) return;
            if (!objectManager.TryGetId(MobileParty.MainParty?.Party, out var myPartyId)) return;
            if (Array.IndexOf(partyIds, myPartyId) < 0) return;

            Logger.Debug("[MapEvent] {Who}: NetworkClosePvpEncounter for my party; closing", Who);

            if (shownDefenderPartyId != null)
                HideWaitingPopup();

            CloseEncounter();
        });
    }

    /// <summary>The defender was pulled into the battle the attacker started; the "hold on" popup has served its purpose.</summary>
    private void Handle_MapEventInvolvedPartiesAdded(MessagePayload<MapEventInvolvedPartiesAdded> payload)
    {
        if (ModInformation.IsServer) return;

        GameThread.Run(() =>
        {
            if (shownDefenderPartyId == null) return;
            if (MobileParty.MainParty?.MapEvent == null) return;

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
    /// side in the pre-battle encounter the attacker just abandoned. For such a joiner we set
    /// <see cref="PlayerEncounter.LeaveEncounter"/> so native removes it from the (now-abandoned) map event on finish.
    /// <see cref="PlayerEncounter.Finish"/>'s postfix (<see cref="Patches.PlayerEncounterPatches"/>) holds the party so
    /// it does not immediately re-engage.
    /// </summary>
    private static void CloseEncounter()
    {
        Logger.Debug("[MapEvent] {Who}: CloseEncounter before: mission={Mission} encounter={Enc} mainPartyMapEvent={Me} menu={Menu}",
            Who,
            MissionState.Current != null,
            PlayerEncounter.Current != null,
            MobileParty.MainParty?.MapEvent != null,
            Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>");

        // A battle mission is open — the party belongs in it; never pull it out.
        if (MissionState.Current != null) return;

        // A joiner stays bound to its map-event side after the attacker abandons the encounter: the abandon tears
        // the event down via FinalizeEvent, which (unlike FinishBattle) does not remove joined parties. The dangling
        // MainParty.MapEvent makes the encounter menu reopen the instant Finish closes it. Detach locally so it stays
        // closed — RemovePartyInternal's client prefix keeps this a local-only cleanup.
        if (MobileParty.MainParty?.MapEvent != null)
            MobileParty.MainParty.Party.MapEventSide = null;

        if (PlayerEncounter.Current != null)
            PlayerEncounter.Finish(true);

        // Finishing the encounter does not itself close the open game menu; exit it explicitly. Mirrors
        // BattleHandler.Handle_NetworkMapEventFinalized, the proven post-battle menu teardown.
        if (Campaign.Current?.CurrentMenuContext != null)
            GameMenu.ExitToLast();

        Logger.Debug("[MapEvent] {Who}: CloseEncounter after: encounter={Enc} mainPartyMapEvent={Me} menu={Menu}",
            Who,
            PlayerEncounter.Current != null,
            MobileParty.MainParty?.MapEvent != null,
            Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>");
    }
}
