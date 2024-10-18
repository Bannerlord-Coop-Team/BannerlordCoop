using Common.Messaging;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Messages;

/// <summary>
/// Internal event for <see cref="BesiegerCamp._besiegerParties" Add/>
/// </summary>
public record BesiegerPartyAdded : IEvent
{
    public BesiegerPartyAdded(BesiegerCamp besiegerCamp, MobileParty mobileParty)
    {
        BesiegerCamp = besiegerCamp;
        BesiegerParty = mobileParty;
    }

    public BesiegerCamp BesiegerCamp { get; }
    public MobileParty BesiegerParty { get; }
}