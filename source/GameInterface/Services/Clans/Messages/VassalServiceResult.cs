using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct VassalServiceResult : ICommand
{
    [ProtoMember(1)]
    public readonly string KingdomId;

    [ProtoMember(2)]
    public readonly bool Accepted;

    [ProtoMember(3)]
    public readonly bool GrantRewards;

    // Refused because the kingdom has maxClansPerKingdom clans.
    [ProtoMember(4)]
    public readonly bool KingdomFull;

    public VassalServiceResult(string kingdomId, bool accepted, bool grantRewards, bool kingdomFull)
    {
        KingdomId = kingdomId;
        Accepted = accepted;
        GrantRewards = grantRewards;
        KingdomFull = kingdomFull;
    }
}
