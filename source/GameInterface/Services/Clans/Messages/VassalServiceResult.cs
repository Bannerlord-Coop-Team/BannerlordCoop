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

    public VassalServiceResult(string kingdomId, bool accepted, bool grantRewards)
    {
        KingdomId = kingdomId;
        Accepted = accepted;
        GrantRewards = grantRewards;
    }
}
