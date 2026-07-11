using System.Collections.Generic;

namespace Missions.Tournaments;

/// <summary>Per-message-stream duplicate/stale guard keyed by the authoritative origin controller.</summary>
public class TournamentMessageSequenceLedger
{
    private readonly object gate = new();
    private readonly Dictionary<string, long> sequences = new();

    public bool TryAccept(string originControllerId, long sequence)
    {
        if (string.IsNullOrEmpty(originControllerId) || sequence <= 0) return false;
        lock (gate)
        {
            if (sequences.TryGetValue(originControllerId, out long last) && sequence <= last)
                return false;
            sequences[originControllerId] = sequence;
            return true;
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            sequences.Clear();
        }
    }
}
