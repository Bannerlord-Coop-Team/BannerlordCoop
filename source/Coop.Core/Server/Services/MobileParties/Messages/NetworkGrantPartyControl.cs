using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>
/// Grants a client the control of a mobile party.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkGrantPartyControl : ICommand
{
    [ProtoMember(1)]
    public string ControllerId { get; }
    [ProtoMember(2)]
    public string PartyId;

    public NetworkGrantPartyControl(string controllerId, string partyId)
    {
        ControllerId = controllerId;
        PartyId = partyId;
    }
}
