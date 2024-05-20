using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Command to change the property HasUnpaidWages of a mobile party.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkHasUnpaidWagesChanged : ICommand
{
    [ProtoMember(1)]
    public float HasUnpaidWages { get; }
    [ProtoMember(2)]
    public string MobilePartyId { get; }

    public NetworkHasUnpaidWagesChanged(float hasUnpaidWages, string mobilePartyId)
    {
        HasUnpaidWages = hasUnpaidWages;
        MobilePartyId = mobilePartyId;
    }
}