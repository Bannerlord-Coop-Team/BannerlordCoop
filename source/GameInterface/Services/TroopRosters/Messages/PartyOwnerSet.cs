using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;

/// <summary>
/// Server-local signal that a <see cref="TroopRoster"/>'s <see cref="TroopRoster.OwnerParty"/> was set.
/// Published from the set_OwnerParty patch; the handler broadcasts <see cref="NetworkPartyOwnerSet"/>.
/// </summary>
internal readonly struct PartyOwnerSet : IEvent
{
    public readonly TroopRoster Roster;
    public readonly PartyBase OwnerParty;

    public PartyOwnerSet(TroopRoster roster, PartyBase ownerParty)
    {
        Roster = roster;
        OwnerParty = ownerParty;
    }
}
