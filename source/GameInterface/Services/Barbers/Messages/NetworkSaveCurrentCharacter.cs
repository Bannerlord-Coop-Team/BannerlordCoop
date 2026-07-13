using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.Barbers.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSaveCurrentCharacter : ICommand
{
    [ProtoMember(1)]
    public readonly string CharacterToChangeId;

    [ProtoMember(2)]
    public readonly BodyProperties CurrentBodyProperties;

    [ProtoMember(3)]
    public readonly int Race;

    [ProtoMember(4)]
    public readonly bool IsFemale;

    public NetworkSaveCurrentCharacter(
        string characterToChangeId,
        BodyProperties currentBodyProperties,
        int race,
        bool isFemale)
    {
        CharacterToChangeId = characterToChangeId;
        CurrentBodyProperties = currentBodyProperties;
        Race = race;
        IsFemale = isFemale;
    }
}
