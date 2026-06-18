using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

/// <summary>
/// [Client -&gt; Server] Requests the server destroy a party that a player destroyed locally (e.g.
/// recruiting surrendering bandits via dialogue). The server applies <c>DestroyPartyAction.Apply</c>
/// with patches live, replicating the destruction to every client via <see cref="NetworkApplyDestroyParty"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestDestroyParty : ICommand
{
    [ProtoMember(1)]
    public readonly string DestroyerPartyId;
    [ProtoMember(2)]
    public readonly string DefeatedPartyId;

    public NetworkRequestDestroyParty(string destroyerPartyId, string defeatedPartyId)
    {
        DestroyerPartyId = destroyerPartyId;
        DefeatedPartyId = defeatedPartyId;
    }
}
