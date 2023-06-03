using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Services.MobileParties.Messages;

internal record PartyTargetPositionAttempted : IEvent
{
    public MobileParty Party { get; }
    public Vec2 NewTargetPosition { get; }
    public PartyTargetPositionAttempted(MobileParty party, Vec2 newTargetPosition)
    {
        Party = party;
        NewTargetPosition = newTargetPosition;
    }
}
