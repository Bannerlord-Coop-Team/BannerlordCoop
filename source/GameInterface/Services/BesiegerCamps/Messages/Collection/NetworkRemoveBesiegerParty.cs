using Common.Messaging;
using GameInterface.Services.BesiegerCamps.Messages.Collection;

namespace GameInterface.Services.BesiegerCamps.Messages;

/// <summary>
/// Command to remove an besieger party on <see cref="BesiegerCamp._besiegerParties"/>
/// </summary>
public record NetworkRemoveBesiegerParty : ICommand
{
    public NetworkRemoveBesiegerParty(BesiegerPartyData besiegerPartyData)
    {
        BesiegerCampId = besiegerPartyData.BesiegerCampId;
        BesiegerPartyId = besiegerPartyData.BesiegerPartyId;
    }

    public string BesiegerCampId { get; }
    public string BesiegerPartyId { get; }
}