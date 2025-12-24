using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Messages.Collection;

/// <summary>
/// Command to add an besieger party on <see cref="BesiegerCamp._besiegerParties"/>
/// </summary>
///
[ProtoContract(SkipConstructor = true)]
public record NetworkAddBesiegerParty : ICommand
{
    public NetworkAddBesiegerParty(BesiegerPartyData besiegerPartyData)
    {
        BesiegerCampId = besiegerPartyData.BesiegerCampId;
        BesiegerPartyId = besiegerPartyData.BesiegerPartyId;
    }

    [ProtoMember(1)]
    public string BesiegerCampId { get; }
    [ProtoMember(2)]
    public string BesiegerPartyId { get; }
}