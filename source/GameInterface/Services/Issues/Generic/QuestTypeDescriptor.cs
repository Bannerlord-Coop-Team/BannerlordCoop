using System;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic;

public abstract class QuestTypeDescriptor
{
    public Type IssueType { get; }
    public Type QuestType { get; }
    public string DisplayName { get; }

    public bool SupportsQuestSolutionAccept { get; }

    public bool SupportsAlternativeAccept { get; }

    public Action<IssueBase> OnGenuineCreation { get; }

    public Action<Hero, string> OnGenuineQuestSolutionAccept { get; }

    public Action<Hero, string> OnGenuineAlternativeAccept { get; }

    public Func<IssueBase, MobileParty, bool> ValidateQuestSuccess { get; }

    public Action<QuestBase> ApplyQuestSuccessConsequence { get; }

    public Func<Hero, Func<Hero, bool>, (bool Accepted, byte[] FieldsBytes)> TryArbitrateQuestSolutionAcceptBytes { get; }

    public Action<Hero, byte[]> MirrorQuestSolutionAcceptBytes { get; }

    public Action<Hero> RejectQuestSolutionAccept { get; }

    public Func<Hero, Func<Hero, bool>, (bool Accepted, byte[] FieldsBytes)> TryArbitrateAlternativeAcceptBytes { get; }

    public Action<Hero, byte[]> MirrorAlternativeAcceptBytes { get; }

    public Action<Hero> RejectAlternativeAccept { get; }

    protected QuestTypeDescriptor(
        Type issueType,
        Type questType,
        string displayName,
        bool supportsQuestSolutionAccept,
        bool supportsAlternativeAccept,
        Action<IssueBase> onGenuineCreation,
        Action<Hero, string> onGenuineQuestSolutionAccept,
        Action<Hero, string> onGenuineAlternativeAccept,
        Func<IssueBase, MobileParty, bool> validateQuestSuccess,
        Action<QuestBase> applyQuestSuccessConsequence,
        Func<Hero, Func<Hero, bool>, (bool, byte[])> tryArbitrateQuestSolutionAcceptBytes,
        Action<Hero, byte[]> mirrorQuestSolutionAcceptBytes,
        Action<Hero> rejectQuestSolutionAccept,
        Func<Hero, Func<Hero, bool>, (bool, byte[])> tryArbitrateAlternativeAcceptBytes,
        Action<Hero, byte[]> mirrorAlternativeAcceptBytes,
        Action<Hero> rejectAlternativeAccept)
    {
        IssueType = issueType ?? throw new ArgumentNullException(nameof(issueType));
        QuestType = questType ?? throw new ArgumentNullException(nameof(questType));
        DisplayName = displayName;
        SupportsQuestSolutionAccept = supportsQuestSolutionAccept;
        SupportsAlternativeAccept = supportsAlternativeAccept;
        OnGenuineCreation = onGenuineCreation;
        OnGenuineQuestSolutionAccept = onGenuineQuestSolutionAccept;
        OnGenuineAlternativeAccept = onGenuineAlternativeAccept;
        ValidateQuestSuccess = validateQuestSuccess;
        ApplyQuestSuccessConsequence = applyQuestSuccessConsequence;
        TryArbitrateQuestSolutionAcceptBytes = tryArbitrateQuestSolutionAcceptBytes;
        MirrorQuestSolutionAcceptBytes = mirrorQuestSolutionAcceptBytes;
        RejectQuestSolutionAccept = rejectQuestSolutionAccept;
        TryArbitrateAlternativeAcceptBytes = tryArbitrateAlternativeAcceptBytes;
        MirrorAlternativeAcceptBytes = mirrorAlternativeAcceptBytes;
        RejectAlternativeAccept = rejectAlternativeAccept;
    }
}

public sealed class QuestTypeDescriptor<TIssue, TQuest> : QuestTypeDescriptor
    where TIssue : IssueBase
    where TQuest : QuestBase
{
    private readonly object _questSolutionAcceptMirrorStrategy;
    private readonly object _alternativeAcceptMirrorStrategy;

    internal QuestTypeDescriptor(
        string displayName,
        object questSolutionAcceptMirrorStrategy,
        object alternativeAcceptMirrorStrategy,
        bool supportsQuestSolutionAccept,
        bool supportsAlternativeAccept,
        Action<TIssue> onGenuineCreation,
        Action<Hero, string> onGenuineQuestSolutionAccept,
        Action<Hero, string> onGenuineAlternativeAccept,
        Func<TIssue, MobileParty, bool> validateQuestSuccess,
        Action<TQuest> applyQuestSuccessConsequence,
        Func<Hero, Func<Hero, bool>, (bool, byte[])> tryArbitrateQuestSolutionAcceptBytes,
        Action<Hero, byte[]> mirrorQuestSolutionAcceptBytes,
        Action<Hero> rejectQuestSolutionAccept,
        Func<Hero, Func<Hero, bool>, (bool, byte[])> tryArbitrateAlternativeAcceptBytes,
        Action<Hero, byte[]> mirrorAlternativeAcceptBytes,
        Action<Hero> rejectAlternativeAccept)
        : base(
            typeof(TIssue),
            typeof(TQuest),
            displayName,
            supportsQuestSolutionAccept,
            supportsAlternativeAccept,
            onGenuineCreation == null ? (Action<IssueBase>)null : issue => { if (issue is TIssue typed) onGenuineCreation(typed); },
            onGenuineQuestSolutionAccept,
            onGenuineAlternativeAccept,
            validateQuestSuccess == null ? (Func<IssueBase, MobileParty, bool>)null : (issue, party) => issue is TIssue typed && validateQuestSuccess(typed, party),
            applyQuestSuccessConsequence == null ? (Action<QuestBase>)null : quest => { if (quest is TQuest typed) applyQuestSuccessConsequence(typed); },
            tryArbitrateQuestSolutionAcceptBytes,
            mirrorQuestSolutionAcceptBytes,
            rejectQuestSolutionAccept,
            tryArbitrateAlternativeAcceptBytes,
            mirrorAlternativeAcceptBytes,
            rejectAlternativeAccept)
    {
        _questSolutionAcceptMirrorStrategy = questSolutionAcceptMirrorStrategy;
        _alternativeAcceptMirrorStrategy = alternativeAcceptMirrorStrategy;
    }

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
        private object _questSolutionAccept;
        private object _alternativeAccept;
        private bool _supportsQuestSolutionAccept;
        private bool _supportsAlternativeAccept;
        private Action<TIssue> _onGenuineCreation;
        private Action<Hero, string> _onGenuineQuestSolutionAccept;
        private Action<Hero, string> _onGenuineAlternativeAccept;
        private Func<TIssue, MobileParty, bool> _validateQuestSuccess;
        private Action<TQuest> _applyQuestSuccessConsequence;
        private Func<Hero, Func<Hero, bool>, (bool, byte[])> _tryArbitrateQuestSolutionAcceptBytes;
        private Action<Hero, byte[]> _mirrorQuestSolutionAcceptBytes;
        private Action<Hero> _rejectQuestSolutionAccept;
        private Func<Hero, Func<Hero, bool>, (bool, byte[])> _tryArbitrateAlternativeAcceptBytes;
        private Action<Hero, byte[]> _mirrorAlternativeAcceptBytes;
        private Action<Hero> _rejectAlternativeAccept;

        internal Builder(string displayName)
        {
            _displayName = displayName;
        }

        public Builder<TIssue, TQuest> WithQuestSolutionAccept()
        {
            _supportsQuestSolutionAccept = true;
            return this;
        }

        public Builder<TIssue, TQuest> WithQuestSolutionAccept<TFields>(IRaceArbitratedAcceptMirrorStrategy<TFields> strategy)
        {
            _supportsQuestSolutionAccept = true;
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

        public Builder<TIssue, TQuest> WithAlternativeAccept()
        {
            _supportsAlternativeAccept = true;
            return this;
        }

        public Builder<TIssue, TQuest> WithAlternativeAccept<TPayload>(IAlternativeAcceptMirrorStrategy<TPayload> strategy)
        {
            _supportsAlternativeAccept = true;
            _alternativeAccept = strategy;

            var handler = new AlternativeAcceptMirrorHandler<TPayload>(strategy);
            _tryArbitrateAlternativeAcceptBytes = (owner, canAccept) =>
            {
                if (!handler.TryArbitrate(owner, canAccept, out var payload)) return (false, null);
                return (true, GenericAcceptFieldsSerializer.Serialize(payload));
            };
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

        public Builder<TIssue, TQuest> WithQuestSuccessConsequence(Action<TQuest> applyQuestSuccessConsequence)
        {
            _applyQuestSuccessConsequence = applyQuestSuccessConsequence;
            return this;
        }

        public QuestTypeDescriptor<TIssue, TQuest> Build()
            => new(_displayName, _questSolutionAccept, _alternativeAccept,
                _supportsQuestSolutionAccept, _supportsAlternativeAccept,
                _onGenuineCreation, _onGenuineQuestSolutionAccept, _onGenuineAlternativeAccept, _validateQuestSuccess,
                _applyQuestSuccessConsequence,
                _tryArbitrateQuestSolutionAcceptBytes, _mirrorQuestSolutionAcceptBytes, _rejectQuestSolutionAccept,
                _tryArbitrateAlternativeAcceptBytes, _mirrorAlternativeAcceptBytes, _rejectAlternativeAccept);
    }
}
