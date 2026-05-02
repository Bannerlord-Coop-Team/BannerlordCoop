using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

/// <summary>
/// Network event notifying that a party has been destroyed on the client.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkDestroyParty : ICommand
{
    [ProtoMember(1)]
    public readonly string PartyId;

    public NetworkDestroyParty(string partyId)
    {
        PartyId = partyId;
    }
}
