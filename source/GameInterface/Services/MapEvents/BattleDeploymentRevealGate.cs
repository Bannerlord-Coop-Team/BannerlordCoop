using System;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Decides when the local player's own-party troops are replicated to the other clients during a coop field
/// battle — the "hidden everywhere until deployed" rule (#4). While the local player is still placing their
/// formations, own-party spawns are WITHHELD from peers (spawned locally so they can be deployed, but not
/// replicated); the host's NPC/AI (not own-party) is NOT withheld, so it shows up frozen on every client during
/// deployment (#1). On the local deployment commit the withheld troops are revealed at their deployed positions
/// (the injected <see cref="IBattleDeploymentRevealSink"/>). Kept free of mission/network types so it is
/// unit-testable.
/// </summary>
public sealed class BattleDeploymentRevealGate
{
    private readonly IBattleDeploymentRevealSink sink;
    private bool committed;

    /// <summary>True once the local player has committed deployment (the own-party troops have been revealed).</summary>
    public bool IsCommitted => committed;

    public BattleDeploymentRevealGate(IBattleDeploymentRevealSink sink)
    {
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    /// <summary>
    /// Whether a freshly-captured owned spawn must be withheld from peers. Own-party troops are withheld until
    /// the local deployment commit (#4); everything else — the host's NPC/AI — is replicated immediately so it
    /// shows frozen on every client during deployment (#1). After commit nothing is withheld: own-party
    /// reinforcements replicate at once like any other owned spawn.
    /// </summary>
    public bool ShouldWithhold(bool isOwnPartyTroop) => !committed && isOwnPartyTroop;

    /// <summary>
    /// The local player committed deployment (Start Battle). Reveal the withheld own-party troops at their
    /// deployed positions. Idempotent — a second commit is a no-op (no duplicate reveal).
    /// </summary>
    public void OnDeploymentCommitted()
    {
        if (committed) return;
        committed = true;
        sink.RevealOwnTroopsAtDeployedPositions();
    }
}
