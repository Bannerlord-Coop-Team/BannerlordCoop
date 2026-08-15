using System;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic;

public static class QuestSuccessProofContext
{
    [ThreadStatic]
    private static byte _current;

    public static byte Current => _current;

    public static void Set(byte value) => _current = value;
}

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

    public Func<IssueBase, byte> CaptureQuestSuccessProof { get; }

    public Func<IssueBase, bool> ValidateQuestCancel { get; }

    public Func<IssueBase, bool> ValidateQuestBetrayal { get; }

    public Func<IssueBase, bool> ValidateQuestFail { get; }

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
        Func<IssueBase, byte> captureQuestSuccessProof,
        Func<IssueBase, bool> validateQuestCancel,
        Func<IssueBase, bool> validateQuestBetrayal,
        Func<IssueBase, bool> validateQuestFail,
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
        CaptureQuestSuccessProof = captureQuestSuccessProof;
        ValidateQuestCancel = validateQuestCancel;
        ValidateQuestBetrayal = validateQuestBetrayal;
        ValidateQuestFail = validateQuestFail;
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
        Func<TIssue, byte> captureQuestSuccessProof,
        Func<TIssue, bool> validateQuestCancel,
        Func<TIssue, bool> validateQuestBetrayal,
        Func<TIssue, bool> validateQuestFail,
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
            NarrowAction(onGenuineCreation),
            onGenuineQuestSolutionAccept,
            onGenuineAlternativeAccept,
            NarrowSuccessValidator(validateQuestSuccess),
            NarrowCapture(captureQuestSuccessProof),
            NarrowPredicate(validateQuestCancel),
            NarrowPredicate(validateQuestBetrayal),
            NarrowPredicate(validateQuestFail),
            NarrowQuestAction(applyQuestSuccessConsequence),
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

    private static Action<IssueBase> NarrowAction(Action<TIssue> action)
        => action == null ? null : issue => { if (issue is TIssue typed) action(typed); };

    private static Func<IssueBase, bool> NarrowPredicate(Func<TIssue, bool> predicate)
        => predicate == null ? null : issue => issue is TIssue typed && predicate(typed);

    private static Func<IssueBase, MobileParty, bool> NarrowSuccessValidator(Func<TIssue, MobileParty, bool> validator)
        => validator == null ? null : (issue, party) => issue is TIssue typed && validator(typed, party);

    private static Func<IssueBase, byte> NarrowCapture(Func<TIssue, byte> capture)
        => capture == null ? null : issue => issue is TIssue typed ? capture(typed) : (byte)0;

    private static Action<QuestBase> NarrowQuestAction(Action<TQuest> action)
        => action == null ? null : quest => { if (quest is TQuest typed) action(typed); };

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
        private Func<TIssue, byte> _captureQuestSuccessProof;
        private Func<TIssue, bool> _validateQuestCancel;
        private Func<TIssue, bool> _validateQuestBetrayal;
        private Func<TIssue, bool> _validateQuestFail;
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

        public Builder<TIssue, TQuest> WithQuestSuccessProofCapture(Func<TIssue, byte> captureQuestSuccessProof)
        {
            _captureQuestSuccessProof = captureQuestSuccessProof;
            return this;
        }

        public Builder<TIssue, TQuest> WithQuestCancelValidation(Func<TIssue, bool> validateQuestCancel)
        {
            _validateQuestCancel = validateQuestCancel;
            return this;
        }

        public Builder<TIssue, TQuest> WithQuestBetrayalValidation(Func<TIssue, bool> validateQuestBetrayal)
        {
            _validateQuestBetrayal = validateQuestBetrayal;
            return this;
        }

        public Builder<TIssue, TQuest> WithQuestFailValidation(Func<TIssue, bool> validateQuestFail)
        {
            _validateQuestFail = validateQuestFail;
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
                _captureQuestSuccessProof,
                _validateQuestCancel, _validateQuestBetrayal, _validateQuestFail,
                _applyQuestSuccessConsequence,
                _tryArbitrateQuestSolutionAcceptBytes, _mirrorQuestSolutionAcceptBytes, _rejectQuestSolutionAccept,
                _tryArbitrateAlternativeAcceptBytes, _mirrorAlternativeAcceptBytes, _rejectAlternativeAccept);
    }
}
