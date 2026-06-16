using Common.Messaging;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;

namespace GameInterface.Services.Locations.Conversations;

/// <summary>
/// Server-side registry of which settlement-location NPC each player is currently talking to, so no two
/// players can hold a conversation with the same NPC at once.
/// </summary>
/// <remarks>
/// Each client runs its own settlement mission, so the "same NPC" is a logical identity - the synced
/// <see cref="TaleWorlds.CampaignSystem.CharacterObject"/> within a <see cref="TaleWorlds.CampaignSystem.Settlements.Locations.Location"/>,
/// keyed by their co-op ids - not a shared agent. When a client opens a conversation with such an NPC it
/// asks the server; the server records the engagement here and refuses any other player's request for the
/// same NPC until the conversation ends. Engagements are keyed per player by the requesting client's
/// <see cref="LiteNetLib.NetPeer"/>, so each player holds at most one at a time. Unlike a map-party hold
/// there is nothing to freeze - this is pure bookkeeping, so it is unit-testable without the game.
/// </remarks>
internal sealed class LocationConversationTracker : IHandler
{
    /// <summary>
    /// DI-wired instance, statically accessible so (static) Harmony patches can reach the object manager.
    /// Set on construction by the auto-activated handler registration.
    /// </summary>
    internal static LocationConversationTracker Instance { get; private set; }

    private readonly object stateLock = new object();
    private readonly Dictionary<string, object> engagerByNpcKey = new Dictionary<string, object>();
    private readonly Dictionary<object, string> npcKeyByEngager = new Dictionary<object, string>();

    private volatile bool isEmpty = true;

    /// <summary>Lock-free fast path: true when no engagements exist.</summary>
    public bool IsEmpty => isEmpty;

    /// <summary>
    /// Object manager shared with the acquire patch so it can resolve NPC/location ids without resolving
    /// the container on every interaction. The tracker itself never uses it.
    /// </summary>
    internal IObjectManager ObjectManager { get; }

    public LocationConversationTracker(IObjectManager objectManager)
    {
        ObjectManager = objectManager;
        Instance = this;
    }

    public void Dispose()
    {
        lock (stateLock)
        {
            engagerByNpcKey.Clear();
            npcKeyByEngager.Clear();
            isEmpty = true;
        }

        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Composes the per-NPC lock key from the NPC's character id and its location id, so the same
    /// character template in two different locations is tracked separately.
    /// </summary>
    public static string ComposeKey(string locationId, string characterId) => $"{locationId}|{characterId}";

    /// <summary>
    /// Begins (or refreshes) <paramref name="engagerKey"/>'s engagement of the given NPC. Fails when
    /// another player currently talks to that NPC, or when this player still has a live engagement with a
    /// different NPC (first approval wins; the old one is released when that conversation ends).
    /// </summary>
    public bool TryBeginEngagement(object engagerKey, string npcKey)
    {
        if (engagerKey == null || npcKey == null) return false;

        lock (stateLock)
        {
            if (engagerByNpcKey.TryGetValue(npcKey, out var existing) && !Equals(existing, engagerKey))
                return false;

            if (npcKeyByEngager.TryGetValue(engagerKey, out var currentNpcKey))
            {
                // Same NPC: refresh. Different NPC: refuse, the live engagement must not be superseded.
                return currentNpcKey == npcKey;
            }

            engagerByNpcKey[npcKey] = engagerKey;
            npcKeyByEngager[engagerKey] = npcKey;
            isEmpty = false;
            return true;
        }
    }

    /// <summary>Ends <paramref name="engagerKey"/>'s engagement, returning the NPC it held.</summary>
    public bool TryEndEngagement(object engagerKey, out string npcKey)
    {
        npcKey = null;

        if (engagerKey == null) return false;

        lock (stateLock)
        {
            if (!npcKeyByEngager.TryGetValue(engagerKey, out npcKey))
                return false;

            npcKeyByEngager.Remove(engagerKey);
            engagerByNpcKey.Remove(npcKey);

            isEmpty = engagerByNpcKey.Count == 0;
            return true;
        }
    }

    /// <summary>True when the NPC is engaged by a player other than <paramref name="engagerKey"/>.</summary>
    public bool IsEngagedByOther(string npcKey, object engagerKey)
    {
        if (npcKey == null) return false;

        lock (stateLock)
        {
            return engagerByNpcKey.TryGetValue(npcKey, out var engager) && !Equals(engager, engagerKey);
        }
    }
}
