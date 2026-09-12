using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSetClanKingdom : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;
    [ProtoMember(2)]
    public readonly string KingdomId;

    public NetworkSetClanKingdom(string clanId, string kingdomId)
    {
        ClanId = clanId;
        KingdomId = kingdomId;
    }
}
