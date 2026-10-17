using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Education.Messages;

public readonly struct EducationStageUpdated : IEvent
{
    public readonly Hero Child;
    public readonly short Target;
    public readonly List<CharacterAttribute> Attributes;
    public readonly List<SkillObject> Skills;

    public EducationStageUpdated(
        Hero child,
        short target,
        List<CharacterAttribute> attributes,
        List<SkillObject> skills)
    {
        Child = child;
        Target = target;
        Attributes = attributes;
        Skills = skills;
    }
}
