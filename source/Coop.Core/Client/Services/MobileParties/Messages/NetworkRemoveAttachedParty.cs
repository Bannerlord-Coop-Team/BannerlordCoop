using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Network message for commanding a removal to the attached parties list
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRemoveAttachedParty : ICommand
{
    [ProtoMember(1)]
    public readonly string PartyId;
    [ProtoMember(2)]
    public readonly string AttachedPartyId;

    public NetworkRemoveAttachedParty(string partyId, string attachedPartyId)
    {
        PartyId = partyId;
        AttachedPartyId = attachedPartyId;
    }
}
