using System;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.CreationCapture;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic;

/// <summary>
/// Non-generic marker so <see cref="QuestTypeRegistry"/> can key on <see cref="IssueType"/> alone: generic
/// runners check <c>QuestTypeRegistry.Get(issueType)</c> first, where null means "not migrated". Every
/// strategy/module a concrete <see cref="QuestTypeDescriptor{TIssue,TQuest}"/> carries is stored type-erased on
/// this base (as <see cref="object"/>) so heterogeneous quest types (different <c>TFields</c>/<c>TPayload</c>
/// shapes per type) can share one registry; the generic subclass hands back strongly-typed accessors for callers
/// that already know their own type parameters.
/// </summary>
public abstract class QuestTypeDescriptor
{
    public Type IssueType { get; }
    public Type QuestType { get; }
    public string DisplayName { get; }

    /// <summary>
    /// A single generic Harmony patch per vanilla hook point (see
    /// <c>Generic.Dispatch.GenericQuestTypeDispatchPatches</c>) looks up this descriptor via
    /// <see cref="QuestTypeRegistry"/> and, only if found, invokes the matching delegate below - replacing a
    /// hand-written <c>if (issue is not ConcreteType) return;</c> guard that would otherwise be duplicated in
    /// every migrated type's own Harmony patch class. The delegate body is still hand-written, per-type logic
    /// (which fields to capture, which concrete wire message to publish); only the decision to run it at all is
    /// registry-driven. Null for a mechanism this type doesn't use.
    /// </summary>
    public Action<IssueBase> OnGenuineCreation { get; }

    /// <summary>[Shape A] Invoked with (owner, controllerId) right after a genuine (non-replayed)
    /// <c>IssueManager.StartIssueQuest</c> succeeds for THIS registered type.</summary>
    public Action<Hero, string> OnGenuineQuestSolutionAccept { get; }

    /// <summary>[Shape B] Invoked with (owner, controllerId) right after a genuine (non-replayed)
    /// <c>IssueBase.StartIssueWithAlternativeSolution</c> runs for THIS registered type.</summary>
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

/// <summary>
/// The full, strongly-typed descriptor for one migrated quest type - the "one place" a migrated type's
/// creation-capture, quest-solution accept-mirror, alternative-accept-mirror and bespoke-module wiring live,
/// replacing the scattered bespoke <c>Interfaces/Messages/Handlers/Patches</c> files a pre-migration type still
/// uses. Built via <see cref="QuestDescriptorBuilder"/>, never constructed directly.
/// </summary>
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

    /// <summary>Strongly-typed accessor for the creation-capture strategy. <typeparamref name="TFields"/> must
    /// match whatever the builder was given - a caller that gets this wrong gets <c>null</c> back, not a cast
    /// exception (see the <c>as</c> cast below), since a migrated type's own handler always knows its own real
    /// TFields shape at the call site.</summary>
    public ICreationCaptureStrategy<TIssue, TFields> GetCreationCapture<TFields>()
        => _creationCaptureStrategy as ICreationCaptureStrategy<TIssue, TFields>;

    public IRaceArbitratedAcceptMirrorStrategy<TFields> GetQuestSolutionAcceptMirror<TFields>()
        => _questSolutionAcceptMirrorStrategy as IRaceArbitratedAcceptMirrorStrategy<TFields>;

    public IAlternativeAcceptMirrorStrategy<TPayload> GetAlternativeAcceptMirror<TPayload>()
        => _alternativeAcceptMirrorStrategy as IAlternativeAcceptMirrorStrategy<TPayload>;
}

/// <summary>Fluent builder for <see cref="QuestTypeDescriptor{TIssue,TQuest}"/> - every step is optional except
/// the initial <see cref="For{TIssue,TQuest}"/> call, since not every migrated type needs every mechanism (e.g.
/// a type with no alternative-solution path at all needs no <see cref="WithAlternativeAccept{TPayload}"/>
/// call).</summary>
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

        /// <summary>Real registry-driven dispatch (see <see cref="QuestTypeDescriptor.OnGenuineCreation"/>'s own
        /// doc comment) - invoked by the single generic Harmony postfix on <c>IssueManager.CreateNewIssue</c>
        /// once dispatch confirms this issue instance is THIS registered type. Body is whatever a pre-migration
        /// type's own dedicated creation-capture Harmony patch used to do after its hand-written type check.</summary>
        public Builder<TIssue, TQuest> WithCreationTrigger(Action<TIssue> onGenuineCreation)
        {
            _onGenuineCreation = onGenuineCreation;
            return this;
        }

        /// <summary>[Shape A] Invoked by the single generic Harmony postfix on <c>IssueManager.StartIssueQuest</c>
        /// once dispatch confirms this issue instance is THIS registered type.</summary>
        public Builder<TIssue, TQuest> WithQuestSolutionAcceptTrigger(Action<Hero, string> onGenuineQuestSolutionAccept)
        {
            _onGenuineQuestSolutionAccept = onGenuineQuestSolutionAccept;
            return this;
        }

        /// <summary>[Shape B] Invoked by the single generic Harmony postfix on
        /// <c>IssueBase.StartIssueWithAlternativeSolution</c> once dispatch confirms this issue instance is THIS
        /// registered type.</summary>
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
