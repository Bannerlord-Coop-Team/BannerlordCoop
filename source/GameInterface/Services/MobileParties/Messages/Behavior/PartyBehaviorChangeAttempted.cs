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
    public readonly MobilePartyAi PartyAi;

    public PartyBehaviorChangeAttempted(MobilePartyAi partyAi)
    {
        PartyAi = partyAi;
    }
}
