using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.HeroDevelopers.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct OpenPerk : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly string PerkId;

    public OpenPerk(string heroId, string perkId)
    {
        HeroId = heroId;
        PerkId = perkId;
    }
}