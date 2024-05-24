using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Command to change the property _partyLastCheckIsPrisoner of a mobile party.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkPartyLastCheckIsPrisonerChanged : ICommand
{
    [ProtoMember(1)]
    public bool PartyLastCheckIsPrisoner { get; }
    [ProtoMember(2)]
    public string MobilePartyId { get; }

    public NetworkPartyLastCheckIsPrisonerChanged(bool partyLastCheckIsPrisoner, string mobilePartyId)
    {
        PartyLastCheckIsPrisoner = partyLastCheckIsPrisoner;
        MobilePartyId = mobilePartyId;
    }
}