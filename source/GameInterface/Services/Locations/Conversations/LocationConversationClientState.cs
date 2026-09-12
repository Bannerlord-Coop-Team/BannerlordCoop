using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Locations.Conversations;

internal readonly struct LocationConversationPending
{
    public Agent Agent { get; }
    public string LocationId { get; }
    public string CharacterId { get; }
    public int Generation { get; }

    public LocationConversationPending(Agent agent, string locationId, string characterId, int generation)
    {
        Agent = agent;
        LocationId = locationId;
        CharacterId = characterId;
        Generation = generation;
    }
}

internal interface ILocationConversationClientState
{
    bool HasPendingOrHeld { get; }
    string HeldNpcKey { get; }

    bool TryBeginPending(Agent agent, string locationId, string characterId, out int generation);
    bool TryTakePending(int generation, out LocationConversationPending pending);
    bool CancelPending(int generation);
    void Hold(string npcKey);
    void ClearHeld();
    bool Clear();
}

/// <summary>
/// Per-client pending and held location-conversation state used by the static Harmony entry points.
/// </summary>
internal class LocationConversationClientState : ILocationConversationClientState
{
    private LocationConversationPending? pending;
    private string heldNpcKey;
    private int requestGeneration;

    public bool HasPendingOrHeld => pending.HasValue || heldNpcKey != null;
    public string HeldNpcKey => heldNpcKey;

    public bool TryBeginPending(Agent agent, string locationId, string characterId, out int generation)
    {
        generation = 0;
        if (agent == null || locationId == null || characterId == null || HasPendingOrHeld) return false;

        generation = ++requestGeneration;
        pending = new LocationConversationPending(agent, locationId, characterId, generation);
        return true;
    }

    public bool TryTakePending(int generation, out LocationConversationPending value)
    {
        value = default;
        if (!pending.HasValue || pending.Value.Generation != generation) return false;

        value = pending.Value;
        pending = null;
        return true;
    }

    public bool CancelPending(int generation)
    {
        if (!pending.HasValue || pending.Value.Generation != generation) return false;
        pending = null;
        return true;
    }

    public void Hold(string npcKey)
    {
        heldNpcKey = npcKey;
    }

    public void ClearHeld()
    {
        heldNpcKey = null;
    }

    public bool Clear()
    {
        bool hadState = HasPendingOrHeld;
        pending = null;
        heldNpcKey = null;
        return hadState;
    }
}
