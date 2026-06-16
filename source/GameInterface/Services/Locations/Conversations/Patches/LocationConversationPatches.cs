using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Locations.Messages.Conversation;
using HarmonyLib;
using SandBox.Conversation.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Conversations.Patches;

/// <summary>
/// Client-side acquire/release for the location-conversation lock. When the player tries to talk to a
/// lock-eligible NPC (a synced hero in the current location), the conversation is held back, the server is
/// asked, and it is started only on approval - or refused with a busy message. The held lock is released
/// when the conversation ends or the mission tears down. Purely-local ambient crowd (non-hero templates)
/// is never gated.
/// </summary>
[HarmonyPatch]
internal static class LocationConversationPatches
{
    private readonly struct PendingConversation
    {
        public readonly Agent Agent;
        public readonly string LocationId;
        public readonly string CharacterId;
        public readonly int Generation;

        public PendingConversation(Agent agent, string locationId, string characterId, int generation)
        {
            Agent = agent;
            LocationId = locationId;
            CharacterId = characterId;
            Generation = generation;
        }
    }

    // Accessed only on the main thread (the interaction, the approval applied via RunOnMainThread, the
    // conversation-end callback, and the mission-end callback all run there).
    private static PendingConversation? pending;
    private static string heldNpcKey;

    // Stamped on each request so a reply for a request the player has since abandoned (left the settlement
    // and started another) is ignored rather than applied to the current pending request.
    private static int requestGeneration;

    [HarmonyPatch(typeof(MissionConversationLogic), nameof(MissionConversationLogic.OnAgentInteraction))]
    [HarmonyPrefix]
    static bool OnAgentInteractionPrefix(MissionConversationLogic __instance, Agent userAgent, Agent agent)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (!ModInformation.IsClient) return true;

        // Not a lock target (ambient crowd, no real interaction, or unresolved ids) -> let vanilla run.
        if (!TryGetLockTarget(__instance, userAgent, agent, out var locationId, out var characterId)) return true;

        // A request is already in flight, or we already hold a conversation: block re-entry without re-asking.
        if (pending.HasValue || heldNpcKey != null) return false;

        var generation = ++requestGeneration;
        pending = new PendingConversation(agent, locationId, characterId, generation);
        MessageBroker.Instance.Publish(agent, new LocationConversationRequested(locationId, characterId, generation));

        // Hold the conversation; it starts when the server approves.
        return false;
    }

    /// <summary>
    /// True only for an interaction this client should gate: a synced hero NPC in the current location, where
    /// vanilla would actually open a conversation and both NPC and location resolve to co-op ids.
    /// </summary>
    private static bool TryGetLockTarget(MissionConversationLogic instance, Agent userAgent, Agent agent,
        out string locationId, out string characterId)
    {
        locationId = null;
        characterId = null;

        // Only synced heroes (notables, companions, quest givers) are a shared identity across clients;
        // ambient crowd shares a culture template and is left unlocked.
        if (!(agent?.Character is CharacterObject character) || !character.IsHero) return false;

        // Mirror vanilla's start guard so we only gate an interaction that would actually open a
        // conversation (distance, enemy, interactability, not already conversing).
        var conversationManager = instance.ConversationManager;
        if (conversationManager == null || conversationManager.IsConversationInProgress) return false;
        if (!instance.IsThereAgentAction(userAgent, agent)) return false;

        var tracker = LocationConversationTracker.Instance;
        var objectManager = tracker?.ObjectManager;
        var location = CampaignMission.Current?.Location;
        if (tracker == null || objectManager == null || location == null) return false;

        return objectManager.TryGetId(character, out characterId) && objectManager.TryGetId(location, out locationId);
    }

    [HarmonyPatch(typeof(MissionConversationLogic), "OnConversationEnd")]
    [HarmonyPostfix]
    static void OnConversationEndPostfix()
    {
        if (heldNpcKey == null) return;

        heldNpcKey = null;
        MessageBroker.Instance.Publish(null, new LocationConversationEnded());
    }

    [HarmonyPatch(typeof(MissionConversationLogic), "OnBehaviorInitialize")]
    [HarmonyPostfix]
    static void OnBehaviorInitializePostfix()
    {
        // A fresh settlement mission is starting. Any lock state left over from a prior mission that did not
        // tear down cleanly (a crash mid-teardown) is stale; release it server-side and clear it so it cannot
        // block the first interaction of this visit.
        ReleaseStaleLock();
    }

    [HarmonyPatch(typeof(MissionConversationLogic), "OnEndMission")]
    [HarmonyPostfix]
    static void OnEndMissionPostfix()
    {
        // The mission tore down (the player left the settlement, etc.). A held conversation already
        // released via OnConversationEnd; anything still set here is a pending request whose reply never
        // arrived - release it server-side and clear local state so a lost reply cannot wedge the next visit.
        ReleaseStaleLock();
    }

    // Drop any stale local lock (pending request or held conversation) and tell the server to release it.
    static void ReleaseStaleLock()
    {
        if (!pending.HasValue && heldNpcKey == null) return;

        pending = null;
        heldNpcKey = null;
        MessageBroker.Instance.Publish(null, new LocationConversationEnded());
    }

    /// <summary>
    /// [Client, main thread] Server approved: start the held-back conversation, unless the approval is for a
    /// request the player has since abandoned (generation mismatch), in which case it is ignored.
    /// </summary>
    internal static void StartApprovedConversation(int generation)
    {
        if (!pending.HasValue || pending.Value.Generation != generation) return;

        var p = pending.Value;
        pending = null;

        // MissionConversationLogic.Current dereferences Mission.Current; the player may have left the scene
        // before the approval arrived. Release the lock and bail rather than crash.
        var logic = Mission.Current != null ? MissionConversationLogic.Current : null;
        var conversationManager = Campaign.Current?.ConversationManager;

        if (logic == null || conversationManager == null || conversationManager.IsConversationInProgress
            || p.Agent == null || !p.Agent.IsActive())
        {
            MessageBroker.Instance.Publish(null, new LocationConversationEnded());
            return;
        }

        // Hold before starting so a conversation that ends synchronously still releases. If the start throws
        // the conversation never opened (no end callback), so release here and clear the hold.
        heldNpcKey = LocationConversationTracker.ComposeKey(p.LocationId, p.CharacterId);
        try
        {
            logic.StartConversation(p.Agent, setActionsInstantly: false);
        }
        catch
        {
            heldNpcKey = null;
            MessageBroker.Instance.Publish(null, new LocationConversationEnded());
            throw;
        }
    }

    /// <summary>
    /// [Client, main thread] Server denied: drop the pending request and report whether it matched, so a stale
    /// denial for an abandoned request neither clears the current pending nor shows a busy message.
    /// </summary>
    internal static bool CancelPending(int generation)
    {
        if (!pending.HasValue || pending.Value.Generation != generation) return false;

        pending = null;
        return true;
    }
}
