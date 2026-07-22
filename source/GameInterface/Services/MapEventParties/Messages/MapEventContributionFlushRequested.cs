using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventParties.Messages;

/// <summary>
/// Requests an inline server flush before a result or teardown boundary continues.
/// </summary>
internal readonly struct MapEventContributionFlushRequested : IEvent
{
    public readonly MapEvent MapEvent;
    public readonly MapEventParty MapEventParty;

    public MapEventContributionFlushRequested(MapEvent mapEvent)
    {
        MapEvent = mapEvent;
        MapEventParty = null;
    }

    public MapEventContributionFlushRequested(MapEventParty mapEventParty)
    {
        MapEvent = null;
        MapEventParty = mapEventParty;
    }
}
