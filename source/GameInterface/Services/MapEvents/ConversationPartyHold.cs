using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using System;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Applies and reverts the server-side hold on an AI party that is in a conversation/encounter with a player,
/// keeping the bookkeeping in <see cref="ConversationPartyTracker"/> and the party's AI state in step.
/// </summary>
/// <remarks>
/// Holding stops the party's current movement (<see cref="MobileParty.SetMoveModeHold"/>) and freezes its decision
/// making (<see cref="MobilePartyAi.DisableAi"/>) so it stays in place while campaign time keeps running for the
/// other players. <c>MobilePartyAi._isDisabled</c> is dynamically synced, so clients see the held state too. A
/// party whose AI was already disabled (e.g. by a quest) is tracked but left untouched. All methods must run on
/// the game's main thread.
/// </remarks>
internal static class ConversationPartyHold
{
    private static readonly TimeSpan BlockedMessageCooldown = TimeSpan.FromSeconds(5);

    private static DateTime lastBlockedMessageUtc = DateTime.MinValue;

    /// <summary>
    /// Shows the local player why their interaction with an engaged party did nothing, at most once per cooldown
    /// so the per-frame blocked checks do not flood the message log. Must run on the game's main thread.
    /// </summary>
    public static void ShowInteractionBlockedMessage()
    {
        var now = DateTime.UtcNow;
        if (now - lastBlockedMessageUtc < BlockedMessageCooldown) return;
        lastBlockedMessageUtc = now;

        InformationManager.DisplayMessage(new InformationMessage(
            "You cannot interact with the party while another player is interacting with it"));
    }

    /// <summary>
    /// Marks the party as engaged by the given player and holds it in place. Fails when another player already
    /// engages the party, or when this player still holds an engagement with a different party (first approval
    /// wins; that hold is released when its conversation ends).
    /// </summary>
    public static bool TryEngage(ConversationPartyTracker tracker, object engagerKey, string engagerPartyId, MobileParty party, string partyId)
    {
        if (tracker == null || party == null) return false;

        var wasAiDisabled = party.Ai?.IsDisabled != false;

        if (!tracker.TryBeginEngagement(engagerKey, engagerPartyId, partyId, wasAiDisabled))
            return false;

        if (!wasAiDisabled)
        {
            party.SetMoveModeHold();
            party.Ai.DisableAi();
        }

        return true;
    }

    /// <summary>Ends the given player's engagement and releases the held party, if any.</summary>
    public static void EndEngagement(ConversationPartyTracker tracker, object engagerKey)
    {
        if (tracker == null) return;

        if (!tracker.TryEndEngagement(engagerKey, out var partyId, out var engagement))
            return;

        ReleaseParty(tracker.ObjectManager, partyId, engagement.WasAiDisabled);
    }

    /// <summary>
    /// [Server] Guard for the host's encounter restart: false when the targeted AI party is already in a
    /// conversation with another player, so the restart must not run.
    /// </summary>
    public static bool CanHostRestartEncounter(PartyBase attackerParty, PartyBase defenderParty)
    {
        var tracker = ConversationPartyTracker.Instance;
        if (tracker == null || tracker.IsEmpty) return true;

        var aiParty = GetAiMobileSide(attackerParty, defenderParty);
        if (aiParty == null) return true;

        // Battle flow: an engagement outlives a conversation that escalated into a battle (it is released at the
        // engager's encounter finish), and battle-related restarts must keep running for such parties.
        if (aiParty.MapEvent != null) return true;

        var objectManager = tracker.ObjectManager;
        if (objectManager == null) return true;
        if (!objectManager.TryGetId(aiParty.Party, out var partyId)) return true;

        return !tracker.IsEngagedByOther(partyId, ConversationPartyTracker.HostEngagerKey);
    }

    /// <summary>
    /// [Server] Marks and holds the AI party of the host's freshly (re)started encounter. Runs after the restart so
    /// the restart's internal <c>PlayerEncounter.Finish</c> (which releases the previous engagement) cannot undo it.
    /// </summary>
    public static void EngageHostEncounteredParty()
    {
        var tracker = ConversationPartyTracker.Instance;
        if (tracker == null) return;

        var party = PlayerEncounter.EncounteredMobileParty;
        if (party == null || party.IsPlayerParty()) return;

        // Battle flow: the map-event rules take over once the party is in a battle.
        if (party.MapEvent != null) return;

        var objectManager = tracker.ObjectManager;
        if (objectManager == null) return;
        if (!objectManager.TryGetId(party.Party, out var partyId)) return;
        if (!objectManager.TryGetId(PartyBase.MainParty, out var hostPartyId)) return;

        TryEngage(tracker, ConversationPartyTracker.HostEngagerKey, hostPartyId, party, partyId);
    }

    /// <summary>[Server] Releases the AI party held for the host's conversation, if any.</summary>
    public static void EndHostEngagement()
    {
        EndEngagement(ConversationPartyTracker.Instance, ConversationPartyTracker.HostEngagerKey);
    }

    /// <summary>
    /// [Server] True when the target party is held in a player's conversation and the interacting party is not the
    /// engaging player's own party, so the map interaction must be blocked.
    /// </summary>
    public static bool IsInteractionBlocked(PartyBase targetParty, MobileParty interactor)
    {
        var tracker = ConversationPartyTracker.Instance;
        if (tracker == null || tracker.IsEmpty) return false;

        var targetMobileParty = targetParty?.MobileParty;
        if (targetMobileParty == null || targetMobileParty.MapEvent != null) return false;

        var objectManager = tracker.ObjectManager;
        if (objectManager == null) return false;
        if (!objectManager.TryGetId(targetParty, out var targetPartyId)) return false;
        if (!tracker.TryGetEngagement(targetPartyId, out var engagement)) return false;

        if (interactor?.Party != null
            && objectManager.TryGetId(interactor.Party, out var interactorId)
            && interactorId == engagement.EngagerPartyId)
            return false;

        return true;
    }

    /// <summary>True when the party is held in a player's conversation (and not already in a battle).</summary>
    public static bool IsInPlayerConversation(MobileParty party)
    {
        var tracker = ConversationPartyTracker.Instance;
        if (tracker == null || tracker.IsEmpty) return false;

        if (party?.Party == null || party.MapEvent != null) return false;

        var objectManager = tracker.ObjectManager;
        if (objectManager == null) return false;
        if (!objectManager.TryGetId(party.Party, out var partyId)) return false;

        return tracker.TryGetEngagement(partyId, out _);
    }

    /// <summary>
    /// Reverts a hold so the party resumes normal AI at its next behavior re-evaluation. Called on engagement end
    /// and by <see cref="ConversationPartyTracker.Dispose"/> for holds that outlive the session.
    /// </summary>
    public static void ReleaseParty(IObjectManager objectManager, string partyId, bool wasAiDisabled)
    {
        if (wasAiDisabled) return;
        if (objectManager == null) return;

        // The party may no longer resolve, e.g. it was destroyed in a battle the conversation escalated into.
        if (!objectManager.TryGetObject(partyId, out PartyBase partyBase)) return;

        var ai = partyBase.MobileParty?.Ai;
        if (ai == null) return;

        ai.EnableAi();
        ai.RethinkAtNextHourlyTick = true;
    }

    /// <summary>The non-player mobile side of an encounter, or null when there is none (e.g. a settlement side).</summary>
    private static MobileParty GetAiMobileSide(PartyBase attackerParty, PartyBase defenderParty)
    {
        var attackerMobile = attackerParty?.MobileParty;
        if (attackerMobile != null && !attackerMobile.IsPlayerParty()) return attackerMobile;

        var defenderMobile = defenderParty?.MobileParty;
        if (defenderMobile != null && !defenderMobile.IsPlayerParty()) return defenderMobile;

        return null;
    }
}
