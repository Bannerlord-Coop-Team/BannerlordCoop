using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkInitializeNewHero : IEvent
{
    [ProtoMember(1)]
    public readonly string HeroId;

    [ProtoMember(2)]
    public readonly TextObject FirstName;

    [ProtoMember(3)]
    public readonly TextObject Name;

    [ProtoMember(8)]
    public readonly string CharacterObjectId;

    public NetworkInitializeNewHero(
        string heroId,
        string characterObjectId,
        TextObject firstName,
        TextObject name)
    {
        HeroId = heroId;
        CharacterObjectId = characterObjectId;
        FirstName = firstName;
        Name = name;
    }
}
