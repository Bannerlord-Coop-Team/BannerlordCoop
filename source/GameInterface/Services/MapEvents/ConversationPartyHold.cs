using GameInterface.Services.ObjectManager;
using System;
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
    /// Marks the party as engaged and holds it in place. The tracker decides whether the engagement can be shared.
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

            // Certain parties such as caravans are still able to make new decisions even with their AI disabled
            // Setting DoNotMakeNewDecisions to true blocks setting new behaviors for these parties
            party.Ai.DoNotMakeNewDecisions = true;
            party.Ai.DisableAi();
        }

        return true;
    }

    /// <summary>Ends the given player's engagement and releases the held party, if any.</summary>
    public static void EndEngagement(ConversationPartyTracker tracker, object engagerKey)
    {
        if (tracker == null) return;

        if (!tracker.TryEndEngagement(engagerKey, out var partyId, out var engagement, out var shouldReleaseParty))
            return;

        if (shouldReleaseParty)
            ReleaseParty(tracker.ObjectManager, partyId, engagement.WasAiDisabled);
    }

    /// <summary>
    /// [Server] True when the target is held and the interacting party is not one of its registered contenders.
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

        string interactorId = null;
        if (interactor?.Party != null)
            objectManager.TryGetId(interactor.Party, out interactorId);

        // Every contender registered for a shared hostile encounter may interact with the target.
        if (tracker.TryGetEngagement(targetPartyId, out _))
            return !tracker.IsEngagerParty(targetPartyId, interactorId);

        // PvP conversation: only the partner (the other player in the conversation) may interact.
        if (tracker.TryGetPvpPartner(targetPartyId, out var partnerId))
            return interactorId != partnerId;

        return false;
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

        return tracker.TryGetEngagement(partyId, out _) || tracker.TryGetPvpPartner(partyId, out _);
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
        ai.DoNotMakeNewDecisions = false;
        ai.RethinkAtNextHourlyTick = true;
    }
}
