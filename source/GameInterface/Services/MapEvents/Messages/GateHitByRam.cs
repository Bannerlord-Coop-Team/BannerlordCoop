using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Local (broker-only) event: a battering ram this client simulates hit a gate. Broadcast with the damage so
/// the host applies it to the authoritative gate and everyone else replays the hit reaction — their gate never
/// runs OnHit. Published by <see cref="Patches.CastleGateHitCapturePatch"/>.
/// </summary>
public record GateHitByRam : IEvent
{
    public CastleGate Gate { get; }
    public BatteringRam Ram { get; }
    public int Damage { get; }

    public GateHitByRam(CastleGate gate, BatteringRam ram, int damage)
    {
        Gate = gate;
        Ram = ram;
        Damage = damage;
    }
}
