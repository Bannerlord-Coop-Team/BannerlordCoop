using System;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.CreationCapture;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic;

public abstract class QuestTypeDescriptor
{
    public Type IssueType { get; }
    public Type QuestType { get; }
    public string DisplayName { get; }

    public Action<IssueBase> OnGenuineCreation { get; }

    public Action<Hero, string> OnGenuineQuestSolutionAccept { get; }

    public Action<Hero, string> OnGenuineAlternativeAccept { get; }

    protected QuestTypeDescriptor(
        Type issueType,
        Type questType,
        string displayName,
        Action<IssueBase> onGenuineCreation,
        Action<Hero, string> onGenuineQuestSolutionAccept,
        Action<Hero, string> onGenuineAlternativeAccept)
    {
        IssueType = issueType ?? throw new ArgumentNullException(nameof(issueType));
        QuestType = questType ?? throw new ArgumentNullException(nameof(questType));
        DisplayName = displayName;
        OnGenuineCreation = onGenuineCreation;
        OnGenuineQuestSolutionAccept = onGenuineQuestSolutionAccept;
        OnGenuineAlternativeAccept = onGenuineAlternativeAccept;
    }
}

public sealed class QuestTypeDescriptor<TIssue, TQuest> : QuestTypeDescriptor
    where TIssue : IssueBase
    where TQuest : QuestBase
{
    private readonly object _creationCaptureStrategy;
    private readonly object _questSolutionAcceptMirrorStrategy;
    private readonly object _alternativeAcceptMirrorStrategy;

    public object BespokeModule { get; }

    internal QuestTypeDescriptor(
        string displayName,
        object creationCaptureStrategy,
        object questSolutionAcceptMirrorStrategy,
        object alternativeAcceptMirrorStrategy,
        object bespokeModule,
        Action<TIssue> onGenuineCreation,
        Action<Hero, string> onGenuineQuestSolutionAccept,
        Action<Hero, string> onGenuineAlternativeAccept)
        : base(
            typeof(TIssue),
            typeof(TQuest),
            displayName,
            onGenuineCreation == null ? (Action<IssueBase>)null : issue => { if (issue is TIssue typed) onGenuineCreation(typed); },
            onGenuineQuestSolutionAccept,
            onGenuineAlternativeAccept)
    {
        _creationCaptureStrategy = creationCaptureStrategy;
        _questSolutionAcceptMirrorStrategy = questSolutionAcceptMirrorStrategy;
        _alternativeAcceptMirrorStrategy = alternativeAcceptMirrorStrategy;
        BespokeModule = bespokeModule;
    }

    public ICreationCaptureStrategy<TIssue, TFields> GetCreationCapture<TFields>()
        => _creationCaptureStrategy as ICreationCaptureStrategy<TIssue, TFields>;

    public IRaceArbitratedAcceptMirrorStrategy<TFields> GetQuestSolutionAcceptMirror<TFields>()
        => _questSolutionAcceptMirrorStrategy as IRaceArbitratedAcceptMirrorStrategy<TFields>;

    public IAlternativeAcceptMirrorStrategy<TPayload> GetAlternativeAcceptMirror<TPayload>()
        => _alternativeAcceptMirrorStrategy as IAlternativeAcceptMirrorStrategy<TPayload>;
}

public static class QuestDescriptorBuilder
{
    public static Builder<TIssue, TQuest> For<TIssue, TQuest>(string displayName)
        where TIssue : IssueBase
        where TQuest : QuestBase
        => new(displayName);

    public sealed class Builder<TIssue, TQuest>
        where TIssue : IssueBase
        where TQuest : QuestBase
    {
        private readonly string _displayName;
        private object _creationCapture;
        private object _questSolutionAccept;
        private object _alternativeAccept;
        private object _bespokeModule;
        private Action<TIssue> _onGenuineCreation;
        private Action<Hero, string> _onGenuineQuestSolutionAccept;
        private Action<Hero, string> _onGenuineAlternativeAccept;

        internal Builder(string displayName)
        {
            _displayName = displayName;
        }

        public Builder<TIssue, TQuest> WithCreationCapture<TFields>(ICreationCaptureStrategy<TIssue, TFields> strategy)
        {
            _creationCapture = strategy;
            return this;
        }

        public Builder<TIssue, TQuest> WithQuestSolutionAccept<TFields>(IRaceArbitratedAcceptMirrorStrategy<TFields> strategy)
        {
            _questSolutionAccept = strategy;
            return this;
        }

        public Builder<TIssue, TQuest> WithAlternativeAccept<TPayload>(IAlternativeAcceptMirrorStrategy<TPayload> strategy)
        {
            _alternativeAccept = strategy;
            return this;
        }

        public Builder<TIssue, TQuest> WithBespokeModule(IQuestBespokeModule<TIssue, TQuest> module)
        {
            _bespokeModule = module;
            return this;
        }

        public Builder<TIssue, TQuest> WithCreationTrigger(Action<TIssue> onGenuineCreation)
        {
            _onGenuineCreation = onGenuineCreation;
            return this;
        }

        public Builder<TIssue, TQuest> WithQuestSolutionAcceptTrigger(Action<Hero, string> onGenuineQuestSolutionAccept)
        {
            _onGenuineQuestSolutionAccept = onGenuineQuestSolutionAccept;
            return this;
        }

        public Builder<TIssue, TQuest> WithAlternativeAcceptTrigger(Action<Hero, string> onGenuineAlternativeAccept)
        {
            _onGenuineAlternativeAccept = onGenuineAlternativeAccept;
            return this;
        }

        public QuestTypeDescriptor<TIssue, TQuest> Build()
            => new(_displayName, _creationCapture, _questSolutionAccept, _alternativeAccept, _bespokeModule,
                _onGenuineCreation, _onGenuineQuestSolutionAccept, _onGenuineAlternativeAccept);
    }
}
