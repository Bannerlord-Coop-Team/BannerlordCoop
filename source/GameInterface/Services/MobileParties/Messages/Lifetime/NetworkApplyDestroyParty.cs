using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages.Lifetime;

/// <summary>
/// [Server -> Client] Replicates a party destruction. <see cref="VictoriousPartyId"/> is null when
/// the party was destroyed with no destroyer (e.g. despawn cleanup such as patrol culling).
/// </summary>
[ProtoContract]
internal readonly struct NetworkApplyDestroyParty : ICommand
{
    [ProtoMember(1)]
    public readonly string VictoriousPartyId;
    [ProtoMember(2)]
    public readonly string DefeatedPartyId;

    public NetworkApplyDestroyParty(string victorousPartyId, string defeatedPartyId)
    {
        VictoriousPartyId = victorousPartyId;
        DefeatedPartyId = defeatedPartyId;
    }
}
