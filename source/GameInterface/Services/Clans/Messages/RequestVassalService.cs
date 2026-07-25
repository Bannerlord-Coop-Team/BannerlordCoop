using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestVassalService : ICommand
{
    [ProtoMember(1)]
    public readonly string KingdomId;

    [ProtoMember(2)]
    public readonly bool GrantRewards;

    public RequestVassalService(string kingdomId, bool grantRewards)
    {
        KingdomId = kingdomId;
        GrantRewards = grantRewards;
    }
}
