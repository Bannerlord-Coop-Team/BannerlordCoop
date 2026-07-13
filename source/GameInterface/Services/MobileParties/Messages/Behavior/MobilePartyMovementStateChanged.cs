using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages.Behavior;

/// <summary>
/// Signals that an authoritative movement command finished mutating a mobile party.
/// </summary>
internal readonly struct MobilePartyMovementStateChanged : IEvent
{
    public MobileParty Party { get; }

    public MobilePartyMovementStateChanged(MobileParty party)
    {
        Party = party;
    }
}
