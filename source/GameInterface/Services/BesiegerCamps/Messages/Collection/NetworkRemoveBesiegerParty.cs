using Common.Messaging;
using GameInterface.Services.BesiegerCamps.Messages.Collection;
using ProtoBuf;

namespace GameInterface.Services.BesiegerCamps.Messages;

/// <summary>
/// Command to remove an besieger party on <see cref="BesiegerCamp._besiegerParties"/>
/// </summary>
/// 
[ProtoContract(SkipConstructor = true)]
public record NetworkRemoveBesiegerParty : ICommand
{
    public NetworkRemoveBesiegerParty(BesiegerPartyData besiegerPartyData)
    {
        BesiegerCampId = besiegerPartyData.BesiegerCampId;
        BesiegerPartyId = besiegerPartyData.BesiegerPartyId;
    }

    [ProtoMember(1)]
    public string BesiegerCampId { get; }
    [ProtoMember(2)]
    public string BesiegerPartyId { get; }
}