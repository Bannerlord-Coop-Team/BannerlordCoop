using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestMercenaryDismissalService : ICommand
{
    [ProtoMember(1)]
    public readonly string KingdomId;
    [ProtoMember(2)]
    public readonly string ClanId;

    public RequestMercenaryDismissalService(string kingdomId, string clanId)
    {
        KingdomId = kingdomId;
        ClanId = clanId;
    }
}
