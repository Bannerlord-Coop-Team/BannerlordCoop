using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages.Leave;

/// <summary>
/// Published on the server after a map event has been finalized and its parties
/// have left it.
/// </summary>
internal readonly struct MapEventFinalized : IEvent
{
    public readonly MapEvent MapEvent;

    public MapEventFinalized(MapEvent mapEvent)
    {
        MapEvent = mapEvent;
    }
}
