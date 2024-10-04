using Common.Messaging;
using GameInterface.Services.BesiegerCamps.Messages.Collection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Messages;

/// <summary>
/// Command to add an besieger party on <see cref="BesiegerCamp._besiegerParties"/>
/// </summary>
public record NetworkAddBesiegerParty : ICommand
{
    public NetworkAddBesiegerParty(BesiegerPartyData besiegerPartyData)
    {
        BesiegerCampId = besiegerPartyData.BesiegerCampId;
        BesiegerPartyId = besiegerPartyData.BesiegerPartyId;
    }

    public string BesiegerCampId { get; }
    public string BesiegerPartyId { get; }
}