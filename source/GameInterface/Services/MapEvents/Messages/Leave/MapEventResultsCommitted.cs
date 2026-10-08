using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages.Leave;

internal readonly struct MapEventResultsCommitted : IEvent
{
    public readonly MapEvent Battle;

    public MapEventResultsCommitted(MapEvent battle) => Battle = battle;
}
