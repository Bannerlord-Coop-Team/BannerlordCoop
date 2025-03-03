using GameInterface.Services.MobileParties.Messages.Data;
using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Event when a party is added from the attached party list
/// </summary>
public record AttachedPartyAdded : GenericEvent<MobileParty, MobileParty>
{
    public AttachedPartyData AttachedPartyData { get; }
    
    // Only used for testing
    public AttachedPartyAdded() : base()
    {}

    public AttachedPartyAdded(MobileParty instance, MobileParty value) : base(instance, value)
    {
        AttachedPartyData = new AttachedPartyData(instance.StringId, value.StringId);
    }

}
