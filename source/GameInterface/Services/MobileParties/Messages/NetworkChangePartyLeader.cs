using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MobileParties.Messages;

/// <summary>
/// [Server -> Client] Replicates a party leader change. <see cref="LeaderHeroId"/> is null when the
/// party is left without a leader (e.g. while its leader is a prisoner).
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkChangePartyLeader : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;
    [ProtoMember(2)]
    public readonly string LeaderHeroId;

    public NetworkChangePartyLeader(string mobilePartyId, string leaderHeroId)
    {
        MobilePartyId = mobilePartyId;
        LeaderHeroId = leaderHeroId;
    }
}
