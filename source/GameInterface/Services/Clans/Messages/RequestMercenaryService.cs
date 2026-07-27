using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestMercenaryService : ICommand
{
    [ProtoMember(1)]
    public readonly string KingdomId;
    [ProtoMember(2)]
    public readonly int AwardMultiplier;
    [ProtoMember(3)]
    public readonly string ClanId;
    public RequestMercenaryService(string kingdomId, int awardMultiplier, string clanId)
    {
        KingdomId = kingdomId;
        AwardMultiplier = awardMultiplier;
        ClanId = clanId;
    }
}
