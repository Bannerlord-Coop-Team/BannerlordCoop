using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRulingClanChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string KingdomId;
    [ProtoMember(2)]
    public readonly string ClanId;

    public NetworkRulingClanChanged(string kingdomId, string clanId)
    {
        KingdomId = kingdomId;
        ClanId = clanId;
    }
}
