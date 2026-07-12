using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.HeroDevelopers.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkOpenPerk : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string PerkId;

    public NetworkOpenPerk(string heroId, string perkId)
    {
        HeroId = heroId;
        PerkId = perkId;
    }
}