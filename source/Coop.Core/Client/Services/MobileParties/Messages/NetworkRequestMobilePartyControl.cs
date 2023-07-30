using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Request control of a mobile party entity.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRequestMobilePartyControl : ICommand
{
    [ProtoMember(1)]
    public string ControllerId;
    [ProtoMember(2)]
    public string PartyId;

    public NetworkRequestMobilePartyControl(string controllerId, string partyId)
    {
        ControllerId = controllerId;
        PartyId = partyId;
    }
}
