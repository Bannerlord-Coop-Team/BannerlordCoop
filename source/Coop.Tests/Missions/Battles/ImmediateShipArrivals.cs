using Missions.Battles;
using System;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// Applies each arrival as it comes, for tests that drive the ship replicator alone, without the naval services tick that
/// applies the arrivals queue; arrival order itself is covered by the queue's own tests.
/// </summary>
public sealed class ImmediateShipArrivals : IBattleShipArrivals
{
    public int Count => 0;

    // Runs the arrival where the queue would have held it.
    public void Enqueue(Action apply, string context) => apply();

    // Nothing waits.
    public void ApplyAll() { }

    // Nothing waits.
    public void Clear() { }
}
