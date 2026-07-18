using Common.Messaging;
using GameInterface.Services.MobileParties.Handlers;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// The game has finished changing party behavior.
/// </summary>
/// <seealso cref="MobilePartyBehaviorHandler"/>
internal readonly struct PartyBehaviorChangeAttempted : IEvent
{
    public readonly MobileParty Party;
    public readonly bool ForcePosition;
    public readonly bool IsCurrentlyAtSea;
    public readonly bool ResetMovementToHold;

    public PartyBehaviorChangeAttempted(
        MobileParty party,
        bool forcePosition = false,
        bool isCurrentlyAtSea = false,
        bool resetMovementToHold = false)
    {
        Party = party;
        ForcePosition = forcePosition;
        IsCurrentlyAtSea = isCurrentlyAtSea;
        ResetMovementToHold = resetMovementToHold;
    }
}
