using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

/// <summary>
/// Network event notifying that a party has been destroyed on the client.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal class NetworkDestroyParty : ICommand
{
    public NetworkDestroyParty(string stringId)
    {
        StringId = stringId;
    }

    [ProtoMember(1)]
    public string StringId { get; }
}
