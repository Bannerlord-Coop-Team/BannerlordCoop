using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages;
internal record MapEventSidesArrayUpdated(MapEvent Instance, MapEventSide Value, int Index) : IEvent
{
    public MapEvent Instance { get; } = Instance;
    public MapEventSide Value { get; } = Value;
    public int Index { get; } = Index;
}
