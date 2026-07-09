using GameInterface.Services;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

internal interface IPlayerPartyHostileEncounterService : IGameAbstraction
{
    bool CanStartHostileEncounter(PartyBase initiatorParty, PartyBase responderParty);
    bool TryStartHostileEncounter(string sessionId, string initiatorPartyId, string responderPartyId, bool responderSurrenders);
}
