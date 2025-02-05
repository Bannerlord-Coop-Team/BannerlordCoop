using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Network message for commanding a removal to the attached parties list
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkRemoveAttachedParty : ICommand
{
    [ProtoMember(1)]
    public AttachedPartyData AttachedPartyData { get; }

    public NetworkRemoveAttachedParty(AttachedPartyData attachedPartyData)
    {
        AttachedPartyData = attachedPartyData;
    }
}
