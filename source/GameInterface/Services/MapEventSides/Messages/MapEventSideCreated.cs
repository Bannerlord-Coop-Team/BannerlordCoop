using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEventSides.Messages;
internal record MapEventSideCreated(MapEventSide Instance, MapEvent MapEvent, BattleSideEnum MissionSide, PartyBase LeaderParty) : IEvent
{
    public MapEventSide Instance { get; } = Instance;
    public MapEvent MapEvent { get; } = MapEvent;
    public BattleSideEnum MissionSide { get; } = MissionSide;
    public PartyBase LeaderParty { get; } = LeaderParty;
}