using Common.Messaging;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Server-side registry of AI parties currently held in a conversation/encounter with a player.
/// </summary>
/// <remarks>
/// In single player the campaign pauses during a conversation, which is what keeps the talked-to party in place and
/// unattackable. Co-op keeps campaign time running, so while a player's encounter with an AI party is open the
/// server records an engagement here. The conversation approval flow and the interaction/AI-attack guards consult
/// the registry so no other player or AI party can interact with the engaged party, and
/// <see cref="ConversationPartyHold"/> reverts the hold when the engagement ends. Engagements are keyed per player -
/// the requesting client's <see cref="LiteNetLib.NetPeer"/>, or <see cref="HostEngagerKey"/> for the host - so each
/// player holds at most one engagement at a time.
/// Aside from <see cref="Dispose"/> - which releases any leftover holds so a campaign that outlives the co-op
/// session is not left with permanently frozen parties - this class is pure bookkeeping, so it is unit-testable
/// without the game.
/// </remarks>
internal sealed class ConversationPartyTracker : IHandler
{
    /// <summary>
    /// DI-wired instance, statically accessible so (static) Harmony patches can reach it. Set on construction by
    /// the auto-activated handler registration.
    /// </summary>
    internal static ConversationPartyTracker Instance { get; private set; }

    /// <summary>Engager key representing the host player; clients are keyed by their <see cref="LiteNetLib.NetPeer"/>.</summary>
    internal static readonly object HostEngagerKey = new object();

    private readonly object stateLock = new object();
    private readonly Dictionary<string, Engagement> engagementsByPartyId = new Dictionary<string, Engagement>();
    private readonly Dictionary<object, string> partyIdsByEngager = new Dictionary<object, string>();

    private volatile bool isEmpty = true;

    /// <summary>Lock-free fast path for per-frame guards: true when no engagements exist.</summary>
    public bool IsEmpty => isEmpty;

    /// <summary>
    /// Object manager shared with the engagement guards, so per-frame checks reuse the DI-wired instance instead
    /// of resolving it from the container on every call. The tracker itself never uses it.
    /// </summary>
    internal IObjectManager ObjectManager { get; }

    public ConversationPartyTracker(IObjectManager objectManager)
    {
        ObjectManager = objectManager;
        Instance = this;
    }

    public void Dispose()
    {
        List<KeyValuePair<string, Engagement>> leftovers;
        lock (stateLock)
        {
            leftovers = new List<KeyValuePair<string, Engagement>>(engagementsByPartyId);
            engagementsByPartyId.Clear();
            partyIdsByEngager.Clear();
            isEmpty = true;
        }

        // The campaign can outlive this co-op session (the container is disposed on leaving the mode while the
        // game stays loaded), and MobilePartyAi._isDisabled is a saveable field that vanilla never re-enables on
        // its own - so any party still held here must be released now or it stays frozen forever.
        foreach (var leftover in leftovers)
            ConversationPartyHold.ReleaseParty(ObjectManager, leftover.Key, leftover.Value.WasAiDisabled);

        if (Instance == this) Instance = null;
    }

    /// <summary>A single player's engagement of an AI party.</summary>
    public readonly struct Engagement
    {
        /// <summary>Key of the engaging player (<see cref="LiteNetLib.NetPeer"/> or <see cref="HostEngagerKey"/>).</summary>
        public readonly object EngagerKey;

        /// <summary>Id of the engaging player's party; interactions from that party stay allowed.</summary>
        public readonly string EngagerPartyId;

        /// <summary>Whether the party's AI was already disabled before the hold, so release preserves that state.</summary>
        public readonly bool WasAiDisabled;

        public Engagement(object engagerKey, string engagerPartyId, bool wasAiDisabled)
        {
            EngagerKey = engagerKey;
            EngagerPartyId = engagerPartyId;
            WasAiDisabled = wasAiDisabled;
        }
    }

    /// <summary>
    /// Begins (or refreshes) <paramref name="engagerKey"/>'s engagement of the given party. Fails when another
    /// player currently engages that party, or when this player still has a live engagement with a different
    /// party: a new conversation must not supersede one whose approval may still be in flight (first approval
    /// wins; the old hold is released when that conversation ends). Re-engaging the same party keeps the
    /// originally recorded <see cref="Engagement.WasAiDisabled"/>.
    /// </summary>
    public bool TryBeginEngagement(object engagerKey, string engagerPartyId, string partyId, bool wasAiDisabled)
    {
        if (engagerKey == null || partyId == null) return false;

        lock (stateLock)
        {
            if (engagementsByPartyId.TryGetValue(partyId, out var existing) && !Equals(existing.EngagerKey, engagerKey))
                return false;

            if (partyIdsByEngager.TryGetValue(engagerKey, out var currentPartyId))
            {
                // Same party: refresh, keeping the original engagement (and its WasAiDisabled).
                return currentPartyId == partyId;
            }

            engagementsByPartyId[partyId] = new Engagement(engagerKey, engagerPartyId, wasAiDisabled);
            partyIdsByEngager[engagerKey] = partyId;
            isEmpty = false;
            return true;
        }
    }

    /// <summary>Ends <paramref name="engagerKey"/>'s engagement, returning the engaged party for release.</summary>
    public bool TryEndEngagement(object engagerKey, out string partyId, out Engagement engagement)
    {
        partyId = null;
        engagement = default;

        if (engagerKey == null) return false;

        lock (stateLock)
        {
            if (!partyIdsByEngager.TryGetValue(engagerKey, out partyId))
                return false;

            partyIdsByEngager.Remove(engagerKey);

            if (engagementsByPartyId.TryGetValue(partyId, out engagement))
                engagementsByPartyId.Remove(partyId);

            isEmpty = engagementsByPartyId.Count == 0;
            return true;
        }
    }

    /// <summary>Gets the engagement holding the given party, if any.</summary>
    public bool TryGetEngagement(string partyId, out Engagement engagement)
    {
        engagement = default;

        if (partyId == null) return false;

        lock (stateLock)
        {
            return engagementsByPartyId.TryGetValue(partyId, out engagement);
        }
    }

    /// <summary>True when the party is engaged by a player other than <paramref name="engagerKey"/>.</summary>
    public bool IsEngagedByOther(string partyId, object engagerKey)
    {
        if (partyId == null) return false;

        lock (stateLock)
        {
            return engagementsByPartyId.TryGetValue(partyId, out var engagement) && !Equals(engagement.EngagerKey, engagerKey);
        }
    }
}
