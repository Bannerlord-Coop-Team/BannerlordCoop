using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.CharacterDevelopers.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkApplyChangesServer : ICommand
{
    [ProtoMember(1)]
    public string HeroId;

    [ProtoMember(2)]
    public List<string> PerkIds;

    [ProtoMember(3)]
    public List<string> AttributeIds;

    [ProtoMember(4)]
    public List<int> AttributeIncreases;

    [ProtoMember(5)]
    public List<string> SkillIds;

    [ProtoMember(6)]
    public List<int> SkillFocusLevels;

    [ProtoMember(7)]
    public List<int> SkillOrgFocusAmounts;

    public NetworkApplyChangesServer(
        string heroId,
        List<string> perkIds,
        List<string> attributeIds,
        List<int> attributeIncreases,
        List<string> skillIds,
        List<int> skillFocusLevels,
        List<int> skillOrgFocusAmounts)
    {
        HeroId = heroId;
        PerkIds = perkIds;
        AttributeIds = attributeIds;
        AttributeIncreases = attributeIncreases;
        SkillIds = skillIds;
        SkillFocusLevels = skillFocusLevels;
        SkillOrgFocusAmounts = skillOrgFocusAmounts;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkApplyChangesClients : ICommand
{
    [ProtoMember(1)]
    public string HeroId;

    [ProtoMember(2)]
    public List<string> PerkIds;

    [ProtoMember(3)]
    public List<string> AttributeIds;

    [ProtoMember(4)]
    public List<int> AttributeIncreases;

    [ProtoMember(5)]
    public List<string> SkillIds;

    [ProtoMember(6)]
    public List<int> SkillFocusLevels;

    [ProtoMember(7)]
    public List<int> SkillOrgFocusAmounts;

    public NetworkApplyChangesClients(NetworkApplyChangesServer cloneObject)
    {
        HeroId = cloneObject.HeroId;
        PerkIds = cloneObject.PerkIds;
        AttributeIds = cloneObject.AttributeIds;
        AttributeIncreases = cloneObject.AttributeIncreases;
        SkillIds = cloneObject.SkillIds;
        SkillFocusLevels = cloneObject.SkillFocusLevels;
        SkillOrgFocusAmounts = cloneObject.SkillOrgFocusAmounts;
    }
}