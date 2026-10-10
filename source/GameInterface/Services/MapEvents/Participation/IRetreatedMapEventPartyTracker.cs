using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Participation;

/// <summary>Tracks player parties that deliberately withdrew from an ongoing shared battle.</summary>
public interface IRetreatedMapEventPartyTracker
{
    void MarkRetreated(MapEvent mapEvent, PartyBase party);
    void MarkReentered(MapEvent mapEvent, PartyBase party);
    bool IsRetreated(MapEvent mapEvent, PartyBase party);
    void Clear(MapEvent mapEvent);
}