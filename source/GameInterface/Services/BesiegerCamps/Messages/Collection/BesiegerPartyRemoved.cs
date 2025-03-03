using GameInterface.Utils.LocalEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Messages;

/// <summary>
/// Internal event for <see cref="BesiegerCamp._besiegerParties" Remove/>
/// </summary>
public record BesiegerPartyRemoved : GenericEvent<BesiegerCamp, MobileParty>
{
    public BesiegerPartyRemoved(BesiegerCamp instance, MobileParty value) : base(instance, value)
    {
    }
}