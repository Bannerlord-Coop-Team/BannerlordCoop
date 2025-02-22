using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Data;
using GameInterface.Utils;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Event when a party is added from the attached party list
/// </summary>
public record AttachedPartyAdded : GenericListEvent<MobileParty, MobileParty>
{
    public AttachedPartyData AttachedPartyData { get; }

    //public AttachedPartyAdded(AttachedPartyData attachedPartyData)
    //{
    //    AttachedPartyData = attachedPartyData;
    //}

    public AttachedPartyAdded(MobileParty instance, MobileParty value) : base(instance, value)
    {
        AttachedPartyData = new AttachedPartyData(instance.StringId, value.StringId);
    }
}
