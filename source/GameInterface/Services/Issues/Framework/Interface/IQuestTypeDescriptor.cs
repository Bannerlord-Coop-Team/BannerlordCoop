using JetBrains.Annotations;
using System;

namespace GameInterface.Services.Issues.Framework.Interface;

public interface IQuestTypeDescriptor
{
    Type IssueType { get; }

    ICreationCaptureStrategy CreationCaptureStrategy { get; }

    IQuestSolutionAcceptStrategy QuestSolutionAcceptStrategy { get; }

    // Null if the quest has no alternative solution
    [CanBeNull] IAlternativeSolutionAcceptStrategy AlternativeSolutionAcceptStrategy { get; }

    // Null if the quest has no outcome branch that needs to be told apart
    [CanBeNull] IFinalizationProofStrategy FinalizationProofStrategy { get; }
}
