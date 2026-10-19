using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Education.Messages;

public readonly struct EducationStageUpdated : IEvent
{
    public readonly Hero Child;
    public readonly short Target;
    public readonly List<string> ChosenOptions;

    public EducationStageUpdated(
        Hero child,
        short target,
        List<string> chosenOptions)
    {
        Child = child;
        Target = target;
        ChosenOptions = chosenOptions;
    }
}
