using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Command to change the field _attachedTo of a mobile party.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkAttachedToChanged : ICommand
{
    [ProtoMember(1)]
    public string AttachedToId { get; }
    [ProtoMember(2)]
    public string MobilePartyId { get; }

    public NetworkAttachedToChanged(string attachedToId, string mobilePartyId)
    {
        AttachedToId = attachedToId;
        MobilePartyId = mobilePartyId;
    }
}