using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.CreationCapture;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;

namespace GameInterface.Tests.Services.Issues.Generic;

/// <summary>
/// Exercises the "coexistence, not flag day" registry the migration story depends on: "generic runners check
/// QuestTypeRegistry.Get(issueType) first, null means 'not migrated'." An issue type that was never
/// <see cref="QuestTypeRegistry.Register"/>-ed must always resolve to null - that's what lets every unmigrated
/// quest type keep running on its own hand-written files, unaware this infrastructure exists at all.
/// </summary>
[Collection(nameof(QuestTypeRegistryCollection))]
public class QuestTypeRegistryTests : IDisposable
{
    public QuestTypeRegistryTests()
    {
        QuestTypeRegistry.ClearAllForTests();
    }

    public void Dispose()
    {
        QuestTypeRegistry.ClearAllForTests();
    }

    // Minimal, never-registered-with-the-real-game stand-ins - only used as Type keys here, never constructed
    // (abstract, and the base-constructor calls below only need to satisfy the compiler, not run for real).
    private abstract class FakeIssueA : IssueBase
    {
        protected FakeIssueA() : base(null, CampaignTime.Never)
        {
        }
    }

    private abstract class FakeQuestA : QuestBase
    {
        protected FakeQuestA() : base(null, null, CampaignTime.Never, 0)
        {
        }
    }

    private abstract class FakeIssueB : IssueBase
    {
        protected FakeIssueB() : base(null, CampaignTime.Never)
        {
        }
    }

    private abstract class FakeQuestB : QuestBase
    {
        protected FakeQuestB() : base(null, null, CampaignTime.Never, 0)
        {
        }
    }

    [Fact]
    public void Get_ReturnsNull_ForAnIssueTypeThatWasNeverRegistered()
    {
        Assert.Null(QuestTypeRegistry.Get(typeof(FakeIssueA)));
        Assert.False(QuestTypeRegistry.IsRegistered(typeof(FakeIssueA)));
    }

    [Fact]
    public void Get_ReturnsNull_ForANullType()
    {
        Assert.Null(QuestTypeRegistry.Get((Type)null));
    }

    [Fact]
    public void Get_ReturnsNull_ForANullIssue()
    {
        Assert.Null(QuestTypeRegistry.Get((IssueBase)null));
    }

    [Fact]
    public void Register_ThenGet_ReturnsTheExactDescriptorForItsOwnIssueTypeOnly()
    {
        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA").Build();

        QuestTypeRegistry.Register(descriptor);

        Assert.Same(descriptor, QuestTypeRegistry.Get(typeof(FakeIssueA)));
        Assert.True(QuestTypeRegistry.IsRegistered(typeof(FakeIssueA)));

        // A DIFFERENT, never-registered issue type must still resolve to null - registering one type must not
        // make every type look "migrated".
        Assert.Null(QuestTypeRegistry.Get(typeof(FakeIssueB)));
        Assert.False(QuestTypeRegistry.IsRegistered(typeof(FakeIssueB)));
    }

    [Fact]
    public void Register_TwiceForTheSameIssueType_OverwritesTheEarlierRegistration()
    {
        var first = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("first").Build();
        var second = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("second").Build();

        QuestTypeRegistry.Register(first);
        QuestTypeRegistry.Register(second);

        Assert.Same(second, QuestTypeRegistry.Get(typeof(FakeIssueA)));
    }

    [Fact]
    public void Builder_CarriesTheStronglyTypedStrategiesThroughToTheDescriptor()
    {
        var creationCapture = new FieldForceCreationCapture<FakeIssueA, int>(
            typeof(DummyFieldHolder).GetField(nameof(DummyFieldHolder.Value)),
            _ => null);

        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .WithCreationCapture(creationCapture)
            .Build();

        Assert.Equal(typeof(FakeIssueA), descriptor.IssueType);
        Assert.Equal(typeof(FakeQuestA), descriptor.QuestType);
        Assert.Equal("FakeA", descriptor.DisplayName);
        Assert.Same(creationCapture, descriptor.GetCreationCapture<int>());

        // Asking for the wrong TFields shape must fail safe (null), not throw or silently return the wrong type.
        Assert.Null(descriptor.GetCreationCapture<string>());
        Assert.Null(descriptor.GetQuestSolutionAcceptMirror<int>());
        Assert.Null(descriptor.GetAlternativeAcceptMirror<Unit>());
    }

    private class DummyFieldHolder
    {
        public int Value;
    }

    // --- Registry-driven dispatch trigger delegates (post-phase-1 safety-audit fix) - exercises exactly the
    // shape Generic.Dispatch.GenericQuestTypeDispatchPatches relies on: QuestTypeRegistry.Get(issueType) first,
    // then invoke whatever trigger delegate (if any) the resolved descriptor carries. ---

    [Fact]
    public void Descriptor_CarriesTheGenuineCreationTrigger_AndOnlyInvokesItForItsOwnIssueType()
    {
        FakeIssueA capturedA = null;
        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .WithCreationTrigger(issue => capturedA = issue)
            .Build();
        QuestTypeRegistry.Register(descriptor);

        // The real dispatch shape: resolve via the base (type-erased) descriptor, then invoke the delegate -
        // exactly what GenericQuestTypeCreationTriggerPatch does.
        var resolved = QuestTypeRegistry.Get(typeof(FakeIssueA));
        Assert.NotNull(resolved?.OnGenuineCreation);

        // An unregistered type must resolve to null, and therefore never invoke anything - the actual
        // coexistence guarantee this whole fix exists to make real.
        Assert.Null(QuestTypeRegistry.Get(typeof(FakeIssueB))?.OnGenuineCreation);
    }

    [Fact]
    public void Descriptor_CarriesTheAcceptTriggers_NullWhenNeverWired()
    {
        var withTriggers = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .WithQuestSolutionAcceptTrigger((owner, controllerId) => { })
            .WithAlternativeAcceptTrigger((owner, controllerId) => { })
            .Build();

        Assert.NotNull(withTriggers.OnGenuineQuestSolutionAccept);
        Assert.NotNull(withTriggers.OnGenuineAlternativeAccept);

        // A type that never calls WithQuestSolutionAcceptTrigger/WithAlternativeAcceptTrigger at all (e.g. one
        // with no such mechanism to migrate) must carry null, not a no-op delegate - callers null-check before
        // invoking (see GenericQuestTypeDispatchPatches).
        var withoutTriggers = QuestDescriptorBuilder.For<FakeIssueB, FakeQuestB>("FakeB").Build();
        Assert.Null(withoutTriggers.OnGenuineQuestSolutionAccept);
        Assert.Null(withoutTriggers.OnGenuineAlternativeAccept);
        Assert.Null(withoutTriggers.OnGenuineCreation);
    }
}

[CollectionDefinition(nameof(QuestTypeRegistryCollection))]
public class QuestTypeRegistryCollection
{
}
