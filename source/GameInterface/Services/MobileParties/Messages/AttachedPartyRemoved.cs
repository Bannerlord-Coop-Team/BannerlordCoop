using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Data;
using GameInterface.Utils;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// Event when a party is removed from the attached party list
/// </summary>
public record AttachedPartyRemoved : GenericListEvent<MobileParty, MobileParty>
{
    public AttachedPartyData AttachedPartyData { get; }

    //public AttachedPartyRemoved(AttachedPartyData attachedPartyData)
    //{
    //    AttachedPartyData = attachedPartyData;
    //}

    public AttachedPartyRemoved(MobileParty instance, MobileParty value) : base(instance, value)
    {
        AttachedPartyData = new AttachedPartyData(instance.StringId, value.StringId);
    }
}
