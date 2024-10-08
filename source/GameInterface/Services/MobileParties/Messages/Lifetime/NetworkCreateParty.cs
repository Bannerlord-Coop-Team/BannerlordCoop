using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

/// <summary>
/// Network event notifying that a party has been created on the client.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal record NetworkCreateParty : IEvent
{
    public NetworkCreateParty(string stringId)
    {
        StringId = stringId;
    }

    [ProtoMember(1)]
    public string StringId { get; }
}
