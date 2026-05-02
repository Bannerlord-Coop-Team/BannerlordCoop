using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Request control of a mobile party entity.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRequestMobilePartyControl : ICommand
{
    [ProtoMember(1)]
    public readonly string ControllerId;
    [ProtoMember(2)]
    public readonly string PartyId;

    public NetworkRequestMobilePartyControl(string controllerId, string partyId)
    {
        ControllerId = controllerId;
        PartyId = partyId;
    }
}
