using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Data;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages;

/// <summary>
/// Network message for commanding an add to the attached parties list
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkAddAttachedParty : ICommand
{
    [ProtoMember(1)]
    public AttachedPartyData AttachedPartyData { get; }

    public NetworkAddAttachedParty(AttachedPartyData attachedPartyData)
    {
        AttachedPartyData = attachedPartyData;
    }
}
