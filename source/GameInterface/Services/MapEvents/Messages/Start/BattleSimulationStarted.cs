using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages.Start;

/// <summary>
/// [Client] Raised when the local player opens the auto-resolve battle simulation screen
/// (<c>MapState.StartBattleSimulation</c>). Forwarded to the server so the server can run the
/// authoritative simulation for this <see cref="TaleWorlds.CampaignSystem.MapEvents.MapEvent"/>.
/// </summary>
internal readonly struct BattleSimulationStarted : IEvent
{
    public readonly MapEvent MapEvent;

    public BattleSimulationStarted(MapEvent mapEvent)
    {
        MapEvent = mapEvent;
    }
}
