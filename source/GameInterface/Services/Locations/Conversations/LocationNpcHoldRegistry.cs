using System.Collections.Generic;

namespace GameInterface.Services.Locations.Conversations;

/// <summary>
/// Client-side record of the conversation-lock holds currently active on location NPCs (SR-040),
/// tracked on EVERY client — not just the acting host. A successor promoted mid-conversation has no
/// other way to know an adopted NPC must stay paused: the hold broadcast predates its authority, and
/// adoption would otherwise un-pause the NPC while the remote conversation is still running.
/// </summary>
public interface ILocationNpcHoldRegistry : IGameAbstraction
{
    void Hold(string locationId, string characterId);
    void Release(string locationId, string characterId);
    bool IsHeld(string locationId, string characterId);
}

/// <inheritdoc cref="ILocationNpcHoldRegistry"/>
internal class LocationNpcHoldRegistry : ILocationNpcHoldRegistry
{
    // Written on the network thread (hold/release broadcasts), read on the game thread (adoption).
    private readonly HashSet<(string locationId, string characterId)> holds = new();
    private readonly object gate = new();

    public void Hold(string locationId, string characterId)
    {
        if (string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(characterId)) return;
        lock (gate) holds.Add((locationId, characterId));
    }

    public void Release(string locationId, string characterId)
    {
        if (string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(characterId)) return;
        lock (gate) holds.Remove((locationId, characterId));
    }

    public bool IsHeld(string locationId, string characterId)
    {
        if (string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(characterId)) return false;
        lock (gate) return holds.Contains((locationId, characterId));
    }
}
