using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;

namespace GameInterface.Tests.Services.Issues.Generic;

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
        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .Build();

        Assert.Equal(typeof(FakeIssueA), descriptor.IssueType);
        Assert.Equal(typeof(FakeQuestA), descriptor.QuestType);
        Assert.Equal("FakeA", descriptor.DisplayName);

        Assert.Null(descriptor.GetQuestSolutionAcceptMirror<int>());
        Assert.Null(descriptor.GetAlternativeAcceptMirror<string>());
    }

    [Fact]
    public void Descriptor_CarriesTheGenuineCreationTrigger_AndOnlyInvokesItForItsOwnIssueType()
    {
        FakeIssueA capturedA = null;
        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .WithCreationTrigger(issue => capturedA = issue)
            .Build();
        QuestTypeRegistry.Register(descriptor);

        var resolved = QuestTypeRegistry.Get(typeof(FakeIssueA));
        Assert.NotNull(resolved?.OnGenuineCreation);

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

        var withoutTriggers = QuestDescriptorBuilder.For<FakeIssueB, FakeQuestB>("FakeB").Build();
        Assert.Null(withoutTriggers.OnGenuineQuestSolutionAccept);
        Assert.Null(withoutTriggers.OnGenuineAlternativeAccept);
        Assert.Null(withoutTriggers.OnGenuineCreation);
    }

    private sealed class FakeQuestSolutionStrategy : IRaceArbitratedAcceptMirrorStrategy<int>
    {
        public bool ReplayCalled;
        public int? MirroredValue;
        public bool RejectCalled;
        public bool CaptureSucceeds = true;
        public int CaptureValue = 42;

        public void ReplayQuestAccepted(Hero owner) => ReplayCalled = true;
        public bool TryCaptureQuestFields(Hero owner, out int fields) { fields = CaptureValue; return CaptureSucceeds; }
        public void MirrorQuestAccepted(Hero owner, int fields) => MirroredValue = fields;
        public void RejectAcceptance(Hero owner) => RejectCalled = true;
    }

    private sealed class FakeAlternativeStrategy : IAlternativeAcceptMirrorStrategy<int>
    {
        public bool ReplayCalled;
        public int? MirroredValue;
        public bool RejectCalled;
        public bool CaptureSucceeds = true;
        public int CaptureValue = 99;

        public void ReplayAlternativeAccepted(Hero owner) => ReplayCalled = true;
        public bool TryCaptureAlternativeFields(Hero owner, out int payload) { payload = CaptureValue; return CaptureSucceeds; }
        public void MirrorAlternativeAccepted(Hero owner, int payload) => MirroredValue = payload;
        public void RejectAcceptance(Hero owner) => RejectCalled = true;
    }

    [Fact]
    public void WithQuestSolutionAccept_WiresTheByteBlobDelegatesThroughToTheStrategy()
    {
        var strategy = new FakeQuestSolutionStrategy();
        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .WithQuestSolutionAccept(strategy)
            .Build();
        var owner = ObjectHelper.SkipConstructor<Hero>();

        var (accepted, fieldsBytes) = descriptor.TryArbitrateQuestSolutionAcceptBytes(owner, _ => true);

        Assert.True(accepted);
        Assert.True(strategy.ReplayCalled);
        Assert.NotNull(fieldsBytes);

        descriptor.MirrorQuestSolutionAcceptBytes(owner, fieldsBytes);
        Assert.Equal(strategy.CaptureValue, strategy.MirroredValue);

        descriptor.RejectQuestSolutionAccept(owner);
        Assert.True(strategy.RejectCalled);
    }

    [Fact]
    public void TryArbitrateQuestSolutionAcceptBytes_CaptureFails_RejectsAndReturnsFalse()
    {
        var strategy = new FakeQuestSolutionStrategy { CaptureSucceeds = false };
        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .WithQuestSolutionAccept(strategy)
            .Build();
        var owner = ObjectHelper.SkipConstructor<Hero>();

        var (accepted, fieldsBytes) = descriptor.TryArbitrateQuestSolutionAcceptBytes(owner, _ => true);

        Assert.False(accepted);
        Assert.Null(fieldsBytes);
        Assert.True(strategy.RejectCalled);
    }

    [Fact]
    public void TryArbitrateQuestSolutionAcceptBytes_CanAcceptReturnsFalse_NeverReplaysOrCaptures()
    {
        var strategy = new FakeQuestSolutionStrategy();
        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .WithQuestSolutionAccept(strategy)
            .Build();
        var owner = ObjectHelper.SkipConstructor<Hero>();

        var (accepted, fieldsBytes) = descriptor.TryArbitrateQuestSolutionAcceptBytes(owner, _ => false);

        Assert.False(accepted);
        Assert.Null(fieldsBytes);
        Assert.False(strategy.ReplayCalled);
    }

    [Fact]
    public void WithAlternativeAccept_WiresTheByteBlobDelegatesThroughToTheStrategy()
    {
        var strategy = new FakeAlternativeStrategy();
        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .WithAlternativeAccept(strategy)
            .Build();
        var owner = ObjectHelper.SkipConstructor<Hero>();

        var (accepted, fieldsBytes) = descriptor.TryArbitrateAlternativeAcceptBytes(owner, _ => true);

        Assert.True(accepted);
        Assert.True(strategy.ReplayCalled);
        Assert.NotNull(fieldsBytes);

        descriptor.MirrorAlternativeAcceptBytes(owner, fieldsBytes);
        Assert.Equal(strategy.CaptureValue, strategy.MirroredValue);

        descriptor.RejectAlternativeAccept(owner);
        Assert.True(strategy.RejectCalled);
    }

    [Fact]
    public void TryArbitrateAlternativeAcceptBytes_CaptureFails_RejectsAndReturnsFalse()
    {
        var strategy = new FakeAlternativeStrategy { CaptureSucceeds = false };
        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .WithAlternativeAccept(strategy)
            .Build();
        var owner = ObjectHelper.SkipConstructor<Hero>();

        var (accepted, fieldsBytes) = descriptor.TryArbitrateAlternativeAcceptBytes(owner, _ => true);

        Assert.False(accepted);
        Assert.Null(fieldsBytes);
        Assert.True(strategy.RejectCalled);
    }

    [Fact]
    public void TryArbitrateAlternativeAcceptBytes_CanAcceptReturnsFalse_NeverReplaysOrCaptures()
    {
        var strategy = new FakeAlternativeStrategy();
        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .WithAlternativeAccept(strategy)
            .Build();
        var owner = ObjectHelper.SkipConstructor<Hero>();

        var (accepted, fieldsBytes) = descriptor.TryArbitrateAlternativeAcceptBytes(owner, _ => false);

        Assert.False(accepted);
        Assert.Null(fieldsBytes);
        Assert.False(strategy.ReplayCalled);
    }

    [Fact]
    public void AcceptByteBlobDelegates_AreNull_WhenNeverWired()
    {
        var descriptor = QuestDescriptorBuilder.For<FakeIssueB, FakeQuestB>("FakeB").Build();

        Assert.Null(descriptor.TryArbitrateQuestSolutionAcceptBytes);
        Assert.Null(descriptor.MirrorQuestSolutionAcceptBytes);
        Assert.Null(descriptor.RejectQuestSolutionAccept);
        Assert.Null(descriptor.TryArbitrateAlternativeAcceptBytes);
        Assert.Null(descriptor.MirrorAlternativeAcceptBytes);
        Assert.Null(descriptor.RejectAlternativeAccept);
    }

    [Fact]
    public void SupportsQuestSolutionAndAlternativeAccept_AreFalse_WhenNeverWired()
    {
        var descriptor = QuestDescriptorBuilder.For<FakeIssueB, FakeQuestB>("FakeB").Build();

        Assert.False(descriptor.SupportsQuestSolutionAccept);
        Assert.False(descriptor.SupportsAlternativeAccept);
    }

    [Fact]
    public void WithQuestSolutionAccept_NoArgOverload_SetsSupportFlagWithoutByteBlobDelegates()
    {
        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .WithQuestSolutionAccept()
            .Build();

        Assert.True(descriptor.SupportsQuestSolutionAccept);
        Assert.Null(descriptor.TryArbitrateQuestSolutionAcceptBytes);
        Assert.Null(descriptor.MirrorQuestSolutionAcceptBytes);
    }

    [Fact]
    public void WithAlternativeAccept_NoArgOverload_SetsSupportFlagWithoutByteBlobDelegates()
    {
        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .WithAlternativeAccept()
            .Build();

        Assert.True(descriptor.SupportsAlternativeAccept);
        Assert.Null(descriptor.TryArbitrateAlternativeAcceptBytes);
        Assert.Null(descriptor.MirrorAlternativeAcceptBytes);
    }

    [Fact]
    public void WithQuestSolutionAccept_ByteBlobOverload_AlsoSetsSupportFlag()
    {
        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .WithQuestSolutionAccept(new FakeQuestSolutionStrategy())
            .Build();

        Assert.True(descriptor.SupportsQuestSolutionAccept);
    }

    [Fact]
    public void WithAlternativeAccept_ByteBlobOverload_AlsoSetsSupportFlag()
    {
        var descriptor = QuestDescriptorBuilder.For<FakeIssueA, FakeQuestA>("FakeA")
            .WithAlternativeAccept(new FakeAlternativeStrategy())
            .Build();

        Assert.True(descriptor.SupportsAlternativeAccept);
    }
}

[CollectionDefinition(nameof(QuestTypeRegistryCollection))]
public class QuestTypeRegistryCollection
{
}
