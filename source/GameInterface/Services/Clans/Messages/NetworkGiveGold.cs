using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkGiveGold : ICommand
{
    [ProtoMember(1)]
    public readonly int Gold;
    [ProtoMember(2)]
    public readonly string HeroId;

    public NetworkGiveGold(int gold, string heroId)
    {
        Gold = gold;
        HeroId = heroId;
    }
}
