using GameInterface.Utils;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Messages;

/// <summary>
/// Internal event for <see cref="BesiegerCamp._besiegerParties" Add/>
/// </summary>
public record BesiegerPartyAdded : GenericListEvent<BesiegerCamp, MobileParty>
{
    public BesiegerPartyAdded(BesiegerCamp instance, MobileParty value) : base(instance, value)
    {
    }
}