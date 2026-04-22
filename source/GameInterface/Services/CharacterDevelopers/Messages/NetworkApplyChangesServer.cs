using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper.PerkSelection;
using TaleWorlds.Library;

namespace GameInterface.Services.CharacterDevelopers.Messages;

[ProtoContract(SkipConstructor = true)]
public class NetworkApplyChangesServer : ICommand
{
    [ProtoMember(1)]
    public string HeroDeveloperId;

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
        string heroDeveloperId,
        List<string> perkIds,
        List<string> attributeIds,
        List<int> attributeIncreases,
        List<string> skillIds,
        List<int> skillFocusLevels,
        List<int> skillOrgFocusAmounts)
    {
        HeroDeveloperId = heroDeveloperId;
        PerkIds = perkIds;
        AttributeIds = attributeIds;
        AttributeIncreases = attributeIncreases;
        SkillIds = skillIds;
        SkillFocusLevels = skillFocusLevels;
        SkillOrgFocusAmounts = skillOrgFocusAmounts;
    }
}