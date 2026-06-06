using GameInterface.Services.MobileParties.Messages.Data;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Event when a party is removed from the attached party list
/// </summary>
public record AttachedPartyRemoved : GenericEvent<MobileParty, MobileParty>
{
    public AttachedPartyRemoved(MobileParty instance, MobileParty value) : base(instance, value)
    {
    }
}
