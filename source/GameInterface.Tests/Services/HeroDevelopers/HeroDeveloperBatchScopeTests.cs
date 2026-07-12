using Common.Messaging;
using GameInterface.Services.HeroDevelopers.Messages;
using GameInterface.Services.HeroDevelopers.Patches;
using System;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.HeroDevelopers;

/// <summary>
/// Verifies the synchronous operation capture used around one hero skill-XP call.
/// </summary>
public class HeroDeveloperBatchScopeTests
{
    [Fact]
    public void Complete_PreservesOperationOrderAndFlags()
    {
        HeroDeveloper developer = Uninitialized<HeroDeveloper>();
        SkillObject skill = Uninitialized<SkillObject>();
        HeroDeveloperBatchScope scope = HeroDeveloperBatchScope.Begin(developer);

        try
        {
            Assert.True(HeroDeveloperBatchScope.TryEnqueue(new RawXpGain(developer, 0.6f, false)));
            Assert.True(HeroDeveloperBatchScope.TryEnqueue(new SkillXpSet(developer, skill, 12.5f)));
            Assert.True(HeroDeveloperBatchScope.TryEnqueue(new SkillLevelChange(developer, skill, 2, true)));

            HeroDeveloperBatch batch = scope.Complete();

            Assert.NotNull(batch);
            Assert.Same(developer, batch.HeroDeveloper);
            Assert.Collection(
                batch.Operations,
                operation =>
                {
                    Assert.Equal(HeroDeveloperOperationType.RawXpGain, operation.Type);
                    Assert.Equal(0.6f, operation.Value);
                    Assert.False(operation.ShouldNotify);
                },
                operation =>
                {
                    Assert.Equal(HeroDeveloperOperationType.SkillXpSet, operation.Type);
                    Assert.Same(skill, operation.SkillObject);
                    Assert.Equal(12.5f, operation.Value);
                },
                operation =>
                {
                    Assert.Equal(HeroDeveloperOperationType.SkillLevelChange, operation.Type);
                    Assert.Same(skill, operation.SkillObject);
                    Assert.Equal(2, operation.ChangeAmount);
                    Assert.True(operation.ShouldNotify);
                });
        }
        finally
        {
            scope.Abort();
        }
    }

    [Fact]
    public void NestedScope_ForSameDeveloper_MergesAtOriginalPosition()
    {
        HeroDeveloper developer = Uninitialized<HeroDeveloper>();
        SkillObject skill = Uninitialized<SkillObject>();
        HeroDeveloperBatchScope outer = HeroDeveloperBatchScope.Begin(developer);

        try
        {
            Assert.True(HeroDeveloperBatchScope.TryEnqueue(new RawXpGain(developer, 1f, false)));

            HeroDeveloperBatchScope inner = HeroDeveloperBatchScope.Begin(developer);
            Assert.True(HeroDeveloperBatchScope.TryEnqueue(new SkillXpSet(developer, skill, 5f)));
            Assert.Null(inner.Complete());

            Assert.True(HeroDeveloperBatchScope.TryEnqueue(new SkillLevelChange(developer, skill, 1, false)));
            HeroDeveloperBatch batch = outer.Complete();

            Assert.Equal(
                new[]
                {
                    HeroDeveloperOperationType.RawXpGain,
                    HeroDeveloperOperationType.SkillXpSet,
                    HeroDeveloperOperationType.SkillLevelChange,
                },
                batch.Operations.Select(operation => operation.Type));
        }
        finally
        {
            outer.Abort();
        }
    }

    [Fact]
    public void Finalizer_WithNestedException_PublishesCapturedOperationsAndRestoresParentScope()
    {
        HeroDeveloper developer = Uninitialized<HeroDeveloper>();
        SkillObject skill = Uninitialized<SkillObject>();
        HeroDeveloperBatchScope outer = HeroDeveloperBatchScope.Begin(developer);
        var failure = new InvalidOperationException("expected test failure");
        HeroDeveloperBatch published = null;
        Action<MessagePayload<HeroDeveloperBatch>> capture = payload => published = payload.What;

        MessageBroker.Instance.Subscribe(capture);
        try
        {
            Assert.True(HeroDeveloperBatchScope.TryEnqueue(new RawXpGain(developer, 1f, false)));

            HeroDeveloperBatchScope inner = HeroDeveloperBatchScope.Begin(developer);
            Assert.True(HeroDeveloperBatchScope.TryEnqueue(new SkillXpSet(developer, skill, 5f)));
            Assert.Same(failure, AddSkillXpBatchPatch.Finalizer(developer, inner, failure));
            Assert.Null(published);

            Assert.True(HeroDeveloperBatchScope.TryEnqueue(new SkillLevelChange(developer, skill, 1, true)));
            Assert.Same(failure, AddSkillXpBatchPatch.Finalizer(developer, outer, failure));

            Assert.NotNull(published);
            Assert.Equal(
                new[]
                {
                    HeroDeveloperOperationType.RawXpGain,
                    HeroDeveloperOperationType.SkillXpSet,
                    HeroDeveloperOperationType.SkillLevelChange,
                },
                published.Operations.Select(operation => operation.Type));
        }
        finally
        {
            outer.Abort();
            MessageBroker.Instance.Unsubscribe(capture);
        }
    }

    private static T Uninitialized<T>() where T : class =>
        (T)FormatterServices.GetUninitializedObject(typeof(T));
}
