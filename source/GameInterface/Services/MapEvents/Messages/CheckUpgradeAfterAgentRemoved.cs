using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages;

public readonly struct CheckUpgradeAfterAgentRemoved : IEvent
{
    public readonly MapEvent MapEvent;
    public readonly PartyBase Party;
    public readonly CharacterObject CharacterObject;
    public readonly BattleSideEnum Side;

    public CheckUpgradeAfterAgentRemoved(
        MapEvent mapEvent,
        PartyBase party,
        CharacterObject characterObject,
        BattleSideEnum side)
    {
        MapEvent = mapEvent;
        Party = party;
        CharacterObject = characterObject;
        Side = side;
    }
}
