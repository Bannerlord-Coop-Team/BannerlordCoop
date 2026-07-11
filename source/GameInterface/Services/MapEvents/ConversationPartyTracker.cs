using Common.Messaging;
using Common.Util;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Server-side registry of AI parties currently held in a conversation/encounter with a player.
/// </summary>
/// <remarks>
/// Each player can engage one target. Hostile contenders may share a target, which stays held until the last
/// engagement ends.
/// </remarks>
internal sealed class ConversationPartyTracker : IHandler
{
    /// <summary>
    /// DI-wired instance, statically accessible so (static) Harmony patches can reach it. Set on construction by
    /// the auto-activated handler registration.
    /// </summary>
    internal static ConversationPartyTracker Instance { get; private set; }

    private readonly object stateLock = new object();
    private readonly Dictionary<string, EngagementGroup> engagementsByPartyId = new Dictionary<string, EngagementGroup>();
    private readonly Dictionary<object, string> partyIdsByEngager = new Dictionary<object, string>(ReferenceObjectComparer.Instance);

    // Player-vs-player conversations: both player parties' ids -> the partner's id. Unlike an AI engagement no party
    // is held/AI-disabled; this just marks the two players unattackable by anyone but each other until the battle
    // map event forms (or the conversation ends).
    private readonly Dictionary<string, string> pvpPartnersByPartyId = new Dictionary<string, string>();

    // Defender party id <-> the defender client's peer, learned from NetworkPvpDefenderShown. The attacker peer is
    // tracked elsewhere (it sent the request); this lets a disconnecting defender be mapped back to its conversation.
    private readonly Dictionary<string, object> pvpPeerByPartyId = new Dictionary<string, object>();
    private readonly Dictionary<object, string> pvpPartyIdByPeer = new Dictionary<object, string>(ReferenceObjectComparer.Instance);

    private volatile bool isEmpty = true;
    private volatile bool pvpIsEmpty = true;
    private bool disposed;

    /// <summary>Lock-free fast path for per-frame guards: true when no AI engagements and no PvP conversations exist.</summary>
    public bool IsEmpty => isEmpty && pvpIsEmpty;

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
        List<KeyValuePair<string, EngagementGroup>> leftovers;
        lock (stateLock)
        {
            if (disposed) return;
            disposed = true;
            leftovers = new List<KeyValuePair<string, EngagementGroup>>(engagementsByPartyId);
            engagementsByPartyId.Clear();
            partyIdsByEngager.Clear();
            pvpPartnersByPartyId.Clear();
            pvpPeerByPartyId.Clear();
            pvpPartyIdByPeer.Clear();
            isEmpty = true;
            pvpIsEmpty = true;
        }

        // The campaign can outlive this co-op session (the container is disposed on leaving the mode while the
        // game stays loaded), and MobilePartyAi._isDisabled is a saveable field that vanilla never re-enables on
        // its own - so any party still held here must be released now or it stays frozen forever.
        using (new AllowedThread())
        {
            foreach (var leftover in leftovers)
                ConversationPartyHold.ReleaseParty(ObjectManager, leftover.Key, leftover.Value.WasAiDisabled);
        }

        if (Instance == this) Instance = null;
    }

    /// <summary>A single player's engagement of an AI party.</summary>
    public readonly struct Engagement
    {
        /// <summary>Key of the engaging player (the requesting client's <see cref="LiteNetLib.NetPeer"/>).</summary>
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
    /// Begins or refreshes an engagement. A player cannot replace a live engagement with a different target, and
    /// every player sharing a target preserves the AI state recorded by its first engagement.
    /// </summary>
    public bool TryBeginEngagement(object engagerKey, string engagerPartyId, string partyId, bool wasAiDisabled)
    {
        if (engagerKey == null || partyId == null) return false;

        lock (stateLock)
        {
            if (disposed) return false;
            if (partyIdsByEngager.TryGetValue(engagerKey, out var currentPartyId))
            {
                return currentPartyId == partyId;
            }

            if (!engagementsByPartyId.TryGetValue(partyId, out var group))
            {
                group = new EngagementGroup(wasAiDisabled);
                engagementsByPartyId[partyId] = group;
            }

            group.Engagements[engagerKey] = new Engagement(engagerKey, engagerPartyId, group.WasAiDisabled);
            partyIdsByEngager[engagerKey] = partyId;
            isEmpty = false;
            return true;
        }
    }

    /// <summary>Ends <paramref name="engagerKey"/>'s engagement, returning the engaged party for release.</summary>
    public bool TryEndEngagement(
        object engagerKey,
        out string partyId,
        out Engagement engagement,
        out bool shouldReleaseParty)
    {
        partyId = null;
        engagement = default;
        shouldReleaseParty = false;

        if (engagerKey == null) return false;

        lock (stateLock)
        {
            if (!partyIdsByEngager.TryGetValue(engagerKey, out partyId))
                return false;

            partyIdsByEngager.Remove(engagerKey);

            if (!engagementsByPartyId.TryGetValue(partyId, out var group) ||
                !group.Engagements.TryGetValue(engagerKey, out engagement))
                return false;

            group.Engagements.Remove(engagerKey);
            if (group.Engagements.Count == 0)
            {
                engagementsByPartyId.Remove(partyId);
                shouldReleaseParty = true;
            }

            isEmpty = engagementsByPartyId.Count == 0;
            return true;
        }
    }

    public bool TryEndEngagement(object engagerKey, out string partyId, out Engagement engagement) =>
        TryEndEngagement(engagerKey, out partyId, out engagement, out _);

    /// <summary>Gets the engagement holding the given party, if any.</summary>
    public bool TryGetEngagement(string partyId, out Engagement engagement)
    {
        engagement = default;

        if (partyId == null) return false;

        lock (stateLock)
        {
            if (!engagementsByPartyId.TryGetValue(partyId, out var group))
                return false;

            foreach (var candidate in group.Engagements.Values)
            {
                engagement = candidate;
                return true;
            }

            return false;
        }
    }

    public bool IsEngagerParty(string partyId, string engagerPartyId)
    {
        if (partyId == null || engagerPartyId == null) return false;

        lock (stateLock)
        {
            if (!engagementsByPartyId.TryGetValue(partyId, out var group))
                return false;

            foreach (var engagement in group.Engagements.Values)
            {
                if (engagement.EngagerPartyId == engagerPartyId)
                    return true;
            }

            return false;
        }
    }

    /// <summary>True when the party is engaged by a player other than <paramref name="engagerKey"/>.</summary>
    public bool IsEngagedByOther(string partyId, object engagerKey)
    {
        if (partyId == null) return false;

        lock (stateLock)
        {
            return engagementsByPartyId.TryGetValue(partyId, out var group) &&
                (engagerKey == null || !group.Engagements.ContainsKey(engagerKey));
        }
    }

    private sealed class EngagementGroup
    {
        public readonly bool WasAiDisabled;
        public readonly Dictionary<object, Engagement> Engagements =
            new Dictionary<object, Engagement>(ReferenceObjectComparer.Instance);

        public EngagementGroup(bool wasAiDisabled)
        {
            WasAiDisabled = wasAiDisabled;
        }
    }

    /// <summary>Marks two player parties as being in a conversation with each other; only they may interact until it ends.</summary>
    public void BeginPvpConversation(string partyIdA, string partyIdB)
    {
        if (partyIdA == null || partyIdB == null) return;

        lock (stateLock)
        {
            if (disposed) return;
            pvpPartnersByPartyId[partyIdA] = partyIdB;
            pvpPartnersByPartyId[partyIdB] = partyIdA;
            pvpIsEmpty = false;
        }
    }

    /// <summary>Ends the PvP conversation containing the given party, releasing both it and its partner.</summary>
    public void EndPvpConversation(string partyId)
    {
        if (partyId == null) return;

        lock (stateLock)
        {
            if (pvpPartnersByPartyId.TryGetValue(partyId, out var partnerId))
            {
                pvpPartnersByPartyId.Remove(partyId);
                pvpPartnersByPartyId.Remove(partnerId);
                RemovePvpPeer(partyId);
                RemovePvpPeer(partnerId);
            }

            pvpIsEmpty = pvpPartnersByPartyId.Count == 0;
        }
    }

    /// <summary>Gets the conversation partner of a party in a PvP conversation, if any.</summary>
    public bool TryGetPvpPartner(string partyId, out string partnerId)
    {
        partnerId = null;
        if (partyId == null) return false;

        lock (stateLock)
        {
            return pvpPartnersByPartyId.TryGetValue(partyId, out partnerId);
        }
    }

    /// <summary>Records the peer of the (defender) client showing the popup for the given party.</summary>
    public void SetPvpDefenderPeer(string partyId, object peer)
    {
        if (partyId == null || peer == null) return;

        lock (stateLock)
        {
            if (disposed) return;
            pvpPeerByPartyId[partyId] = peer;
            pvpPartyIdByPeer[peer] = partyId;
        }
    }

    /// <summary>Maps a peer back to the PvP party it is the defender of, if any (for disconnect handling).</summary>
    public bool TryGetPvpPartyByPeer(object peer, out string partyId)
    {
        partyId = null;
        if (peer == null) return false;

        lock (stateLock)
        {
            return pvpPartyIdByPeer.TryGetValue(peer, out partyId);
        }
    }

    // Drops the peer<->party mapping for a party leaving a PvP conversation. Must be called under stateLock.
    private void RemovePvpPeer(string partyId)
    {
        if (pvpPeerByPartyId.TryGetValue(partyId, out var peer))
        {
            pvpPeerByPartyId.Remove(partyId);
            pvpPartyIdByPeer.Remove(peer);
        }
    }

    private sealed class ReferenceObjectComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceObjectComparer Instance = new ReferenceObjectComparer();

        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
