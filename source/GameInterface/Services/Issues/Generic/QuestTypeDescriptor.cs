using System;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.CreationCapture;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic;

public abstract class QuestTypeDescriptor
{
    public Type IssueType { get; }
    public Type QuestType { get; }
    public string DisplayName { get; }

    public Action<IssueBase> OnGenuineCreation { get; }

    public Action<Hero, string> OnGenuineQuestSolutionAccept { get; }

    public Action<Hero, string> OnGenuineAlternativeAccept { get; }

    public Func<IssueBase, MobileParty, bool> ValidateQuestSuccess { get; }

    public Func<Hero, Func<Hero, bool>, (bool Accepted, byte[] FieldsBytes)> TryArbitrateQuestSolutionAcceptBytes { get; }

    public Action<Hero, byte[]> MirrorQuestSolutionAcceptBytes { get; }

    public Action<Hero> RejectQuestSolutionAccept { get; }

    public Action<Hero, byte[]> MirrorAlternativeAcceptBytes { get; }

    public Action<Hero> RejectAlternativeAccept { get; }

    protected QuestTypeDescriptor(
        Type issueType,
        Type questType,
        string displayName,
        Action<IssueBase> onGenuineCreation,
        Action<Hero, string> onGenuineQuestSolutionAccept,
        Action<Hero, string> onGenuineAlternativeAccept,
        Func<IssueBase, MobileParty, bool> validateQuestSuccess,
        Func<Hero, Func<Hero, bool>, (bool, byte[])> tryArbitrateQuestSolutionAcceptBytes,
        Action<Hero, byte[]> mirrorQuestSolutionAcceptBytes,
        Action<Hero> rejectQuestSolutionAccept,
        Action<Hero, byte[]> mirrorAlternativeAcceptBytes,
        Action<Hero> rejectAlternativeAccept)
    {
        IssueType = issueType ?? throw new ArgumentNullException(nameof(issueType));
        QuestType = questType ?? throw new ArgumentNullException(nameof(questType));
        DisplayName = displayName;
        OnGenuineCreation = onGenuineCreation;
        OnGenuineQuestSolutionAccept = onGenuineQuestSolutionAccept;
        OnGenuineAlternativeAccept = onGenuineAlternativeAccept;
        ValidateQuestSuccess = validateQuestSuccess;
        TryArbitrateQuestSolutionAcceptBytes = tryArbitrateQuestSolutionAcceptBytes;
        MirrorQuestSolutionAcceptBytes = mirrorQuestSolutionAcceptBytes;
        RejectQuestSolutionAccept = rejectQuestSolutionAccept;
        MirrorAlternativeAcceptBytes = mirrorAlternativeAcceptBytes;
        RejectAlternativeAccept = rejectAlternativeAccept;
    }
}

public sealed class QuestTypeDescriptor<TIssue, TQuest> : QuestTypeDescriptor
    where TIssue : IssueBase
    where TQuest : QuestBase
{
    private readonly object _creationCaptureStrategy;
    private readonly object _questSolutionAcceptMirrorStrategy;
    private readonly object _alternativeAcceptMirrorStrategy;

    internal QuestTypeDescriptor(
        string displayName,
        object creationCaptureStrategy,
        object questSolutionAcceptMirrorStrategy,
        object alternativeAcceptMirrorStrategy,
        Action<TIssue> onGenuineCreation,
        Action<Hero, string> onGenuineQuestSolutionAccept,
        Action<Hero, string> onGenuineAlternativeAccept,
        Func<TIssue, MobileParty, bool> validateQuestSuccess,
        Func<Hero, Func<Hero, bool>, (bool, byte[])> tryArbitrateQuestSolutionAcceptBytes,
        Action<Hero, byte[]> mirrorQuestSolutionAcceptBytes,
        Action<Hero> rejectQuestSolutionAccept,
        Action<Hero, byte[]> mirrorAlternativeAcceptBytes,
        Action<Hero> rejectAlternativeAccept)
        : base(
            typeof(TIssue),
            typeof(TQuest),
            displayName,
            onGenuineCreation == null ? (Action<IssueBase>)null : issue => { if (issue is TIssue typed) onGenuineCreation(typed); },
            onGenuineQuestSolutionAccept,
            onGenuineAlternativeAccept,
            validateQuestSuccess == null ? (Func<IssueBase, MobileParty, bool>)null : (issue, party) => issue is TIssue typed && validateQuestSuccess(typed, party),
            tryArbitrateQuestSolutionAcceptBytes,
            mirrorQuestSolutionAcceptBytes,
            rejectQuestSolutionAccept,
            mirrorAlternativeAcceptBytes,
            rejectAlternativeAccept)
    {
        _creationCaptureStrategy = creationCaptureStrategy;
        _questSolutionAcceptMirrorStrategy = questSolutionAcceptMirrorStrategy;
        _alternativeAcceptMirrorStrategy = alternativeAcceptMirrorStrategy;
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
        private Action<TIssue> _onGenuineCreation;
        private Action<Hero, string> _onGenuineQuestSolutionAccept;
        private Action<Hero, string> _onGenuineAlternativeAccept;
        private Func<TIssue, MobileParty, bool> _validateQuestSuccess;
        private Func<Hero, Func<Hero, bool>, (bool, byte[])> _tryArbitrateQuestSolutionAcceptBytes;
        private Action<Hero, byte[]> _mirrorQuestSolutionAcceptBytes;
        private Action<Hero> _rejectQuestSolutionAccept;
        private Action<Hero, byte[]> _mirrorAlternativeAcceptBytes;
        private Action<Hero> _rejectAlternativeAccept;

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

            var handler = new RaceArbitratedAcceptMirrorHandler<TFields>(strategy);
            _tryArbitrateQuestSolutionAcceptBytes = (owner, canAccept) =>
            {
                if (!handler.TryArbitrate(owner, canAccept, out var fields)) return (false, null);
                return (true, GenericAcceptFieldsSerializer.Serialize(fields));
            };
            _mirrorQuestSolutionAcceptBytes = (owner, bytes) =>
                handler.Mirror(owner, GenericAcceptFieldsSerializer.Deserialize<TFields>(bytes));
            _rejectQuestSolutionAccept = owner => handler.Reject(owner);

            return this;
        }

        public Builder<TIssue, TQuest> WithAlternativeAccept<TPayload>(IAlternativeAcceptMirrorStrategy<TPayload> strategy)
        {
            _alternativeAccept = strategy;

            var handler = new AlternativeAcceptMirrorHandler<TPayload>(strategy);
            _mirrorAlternativeAcceptBytes = (owner, bytes) =>
                handler.Mirror(owner, GenericAcceptFieldsSerializer.Deserialize<TPayload>(bytes));
            _rejectAlternativeAccept = owner => handler.Reject(owner);

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

        public Builder<TIssue, TQuest> WithQuestSuccessValidation(Func<TIssue, MobileParty, bool> validateQuestSuccess)
        {
            _validateQuestSuccess = validateQuestSuccess;
            return this;
        }

        public QuestTypeDescriptor<TIssue, TQuest> Build()
            => new(_displayName, _creationCapture, _questSolutionAccept, _alternativeAccept,
                _onGenuineCreation, _onGenuineQuestSolutionAccept, _onGenuineAlternativeAccept, _validateQuestSuccess,
                _tryArbitrateQuestSolutionAcceptBytes, _mirrorQuestSolutionAcceptBytes, _rejectQuestSolutionAccept,
                _mirrorAlternativeAcceptBytes, _rejectAlternativeAccept);
    }
}
