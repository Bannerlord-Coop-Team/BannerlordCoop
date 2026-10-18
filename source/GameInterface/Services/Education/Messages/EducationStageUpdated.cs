using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Education.Messages;

public readonly struct EducationStageUpdated : IEvent
{
    public readonly Hero Child;
    public readonly short Target;
    public readonly List<string> ChosenOptions;
    public readonly List<SkillObject> Skills;

    public EducationStageUpdated(
        Hero child,
        short target,
        List<string> chosenOptions,
        List<SkillObject> skills)
    {
        Child = child;
        Target = target;
        ChosenOptions = chosenOptions;
        Skills = skills;
    }
}
