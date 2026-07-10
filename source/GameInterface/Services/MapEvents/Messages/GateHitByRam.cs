using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Local (broker-only) event: a battering ram struck a closed gate hard enough for the host to play the gate's
/// hit reaction (door/plank flinch + impact sound). Broadcast so peers replay that reaction — their gate never
/// runs OnHit, so its OnHitTaken handler never fires. Published by <see cref="Patches.CastleGateHitCapturePatch"/>.
/// </summary>
public record GateHitByRam : IEvent
{
    public CastleGate Gate { get; }

    public GateHitByRam(CastleGate gate)
    {
        Gate = gate;
    }
}
