using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Local (broker-only) event: a battering ram on the mission host started a strike (the wind-up/hit swing),
/// with the crew power stage the host used. Broadcast so peers play the ram body animation, which is otherwise
/// host-local — a peer's ram is unmanned (its TickAux is blocked) so it never strikes locally. Published by
/// <see cref="Patches.BatteringRamHitCapturePatch"/>.
/// </summary>
public record RamHitStarted : IEvent
{
    public BatteringRam Ram { get; }
    public int PowerStage { get; }
    public float Progress { get; }

    public RamHitStarted(BatteringRam ram, int powerStage, float progress)
    {
        Ram = ram;
        PowerStage = powerStage;
        Progress = progress;
    }
}
