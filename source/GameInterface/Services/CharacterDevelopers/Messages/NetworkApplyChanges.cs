using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.CharacterDevelopers.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkApplyChanges : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroDeveloperId;

    [ProtoMember(2)]
    public readonly List<string> PerkIds;

    [ProtoMember(3)]
    public readonly List<string> AttributeIds;

    [ProtoMember(4)]
    public readonly List<int> AttributeIncreases;

    [ProtoMember(5)]
    public readonly List<string> SkillIds;

    [ProtoMember(6)]
    public readonly List<int> SkillFocusLevels;

    public NetworkApplyChanges(
        string heroDeveloperId,
        List<string> perkIds,
        List<string> attributeIds,
        List<int> attributeIncreases,
        List<string> skillIds,
        List<int> skillFocusLevels)
    {
        HeroDeveloperId = heroDeveloperId;
        PerkIds = perkIds;
        AttributeIds = attributeIds;
        AttributeIncreases = attributeIncreases;
        SkillIds = skillIds;
        SkillFocusLevels = skillFocusLevels;
    }
}
