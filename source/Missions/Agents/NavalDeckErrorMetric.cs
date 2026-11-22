using System;

namespace Missions.Agents;

/// <summary>The deck error diagnostic: the last and largest puppet-to-target error on a deck, skipping a teleport's or a deck
/// placement's sample, which still measures the puppet's old spot (R43: 29-64 m, then millimetres).</summary>
internal sealed class NavalDeckErrorMetric
{
    public float Last { get; private set; }
    public float Max { get; private set; }
    public long Skipped { get; private set; }

    /// <summary>Records one sample; a transition's (a teleport or a placement on a new deck) is only counted as skipped.</summary>
    public void Record(float error, bool isTransition)
    {
        if (isTransition)
        {
            Skipped++;
            return;
        }

        Last = error;
        Max = Math.Max(Max, error);
    }

    /// <summary>Starts over for a new battle.</summary>
    public void Reset()
    {
        Last = Max = 0f;
        Skipped = 0;
    }
}
