using Common.Messaging;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;

namespace GameInterface.Services.Locations.Conversations;

internal interface ILocationConversationTracker
{
    bool IsEmpty { get; }
    IObjectManager ObjectManager { get; }

    bool TryBeginEngagement(object engagerKey, string engagerNpcKey, string targetNpcKey);
    bool TryBeginEngagement(object engagerKey, object responderKey, string engagerNpcKey, string targetNpcKey);
    bool TryEndEngagement(object participantKey, out string npcKey);
    bool TryEndEngagement(object participantKey, out string npcKey, out object engagerKey);
    bool TryGetEngagement(object participantKey, out string npcKey);
    bool IsEngagedByOther(string npcKey, object engagerKey);
}

/// <summary>
/// Server-side registry of settlement-location conversations. Both logical characters are reserved and
/// player-to-player sessions are indexed by both peers so either participant can release the lock.
/// </summary>
internal sealed class LocationConversationTracker : ILocationConversationTracker, IHandler
{
    private sealed class Engagement
    {
        public object EngagerKey { get; }
        public object ResponderKey { get; }
        public string EngagerNpcKey { get; }
        public string TargetNpcKey { get; }

        public Engagement(
            object engagerKey,
            object responderKey,
            string engagerNpcKey,
            string targetNpcKey)
        {
            EngagerKey = engagerKey;
            ResponderKey = responderKey;
            EngagerNpcKey = engagerNpcKey;
            TargetNpcKey = targetNpcKey;
        }
    }

    private readonly object stateLock = new object();
    private readonly Dictionary<string, Engagement> engagementByNpcKey = new Dictionary<string, Engagement>();
    private readonly Dictionary<object, Engagement> engagementByParticipant = new Dictionary<object, Engagement>();

    private volatile bool isEmpty = true;

    public bool IsEmpty => isEmpty;
    public IObjectManager ObjectManager { get; }

    public LocationConversationTracker(IObjectManager objectManager)
    {
        ObjectManager = objectManager;
    }

    public void Dispose()
    {
        lock (stateLock)
        {
            engagementByNpcKey.Clear();
            engagementByParticipant.Clear();
            isEmpty = true;
        }
    }

    public static string ComposeKey(string locationId, string characterId) => $"{locationId}|{characterId}";

    public bool TryBeginEngagement(object engagerKey, string engagerNpcKey, string targetNpcKey)
    {
        return TryBeginEngagement(engagerKey, null, engagerNpcKey, targetNpcKey);
    }

    public bool TryBeginEngagement(
        object engagerKey,
        object responderKey,
        string engagerNpcKey,
        string targetNpcKey)
    {
        if (engagerKey == null || engagerNpcKey == null || targetNpcKey == null) return false;
        if (responderKey != null && Equals(engagerKey, responderKey)) return false;

        lock (stateLock)
        {
            if (engagementByParticipant.TryGetValue(engagerKey, out var current))
            {
                return Equals(current.EngagerKey, engagerKey) &&
                       Equals(current.ResponderKey, responderKey) &&
                       current.EngagerNpcKey == engagerNpcKey &&
                       current.TargetNpcKey == targetNpcKey;
            }

            if (responderKey != null && engagementByParticipant.ContainsKey(responderKey)) return false;
            if (engagementByNpcKey.ContainsKey(engagerNpcKey)) return false;
            if (engagementByNpcKey.ContainsKey(targetNpcKey)) return false;

            var engagement = new Engagement(engagerKey, responderKey, engagerNpcKey, targetNpcKey);
            engagementByNpcKey[engagerNpcKey] = engagement;
            engagementByNpcKey[targetNpcKey] = engagement;
            engagementByParticipant[engagerKey] = engagement;
            if (responderKey != null)
                engagementByParticipant[responderKey] = engagement;

            isEmpty = false;
            return true;
        }
    }

    public bool TryEndEngagement(object participantKey, out string npcKey)
    {
        return TryEndEngagement(participantKey, out npcKey, out _);
    }

    public bool TryEndEngagement(object participantKey, out string npcKey, out object engagerKey)
    {
        npcKey = null;
        engagerKey = null;
        if (participantKey == null) return false;

        lock (stateLock)
        {
            if (!engagementByParticipant.TryGetValue(participantKey, out var engagement)) return false;

            npcKey = engagement.TargetNpcKey;
            engagerKey = engagement.EngagerKey;
            engagementByParticipant.Remove(engagement.EngagerKey);
            if (engagement.ResponderKey != null)
                engagementByParticipant.Remove(engagement.ResponderKey);
            engagementByNpcKey.Remove(engagement.EngagerNpcKey);
            engagementByNpcKey.Remove(engagement.TargetNpcKey);

            isEmpty = engagementByParticipant.Count == 0;
            return true;
        }
    }

    public bool TryGetEngagement(object participantKey, out string npcKey)
    {
        npcKey = null;
        if (participantKey == null) return false;

        lock (stateLock)
        {
            if (!engagementByParticipant.TryGetValue(participantKey, out var engagement)) return false;
            npcKey = engagement.TargetNpcKey;
            return true;
        }
    }

    public bool IsEngagedByOther(string npcKey, object engagerKey)
    {
        if (npcKey == null) return false;

        lock (stateLock)
        {
            return engagementByNpcKey.TryGetValue(npcKey, out var engagement) &&
                   !Equals(engagement.EngagerKey, engagerKey);
        }
    }
}
