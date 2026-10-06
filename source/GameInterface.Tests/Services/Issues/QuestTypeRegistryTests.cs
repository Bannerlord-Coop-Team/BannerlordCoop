using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Registries;
using Moq;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class QuestTypeRegistryTests
{
    private class FirstIssue
    {
    }

    private class SecondIssue
    {
    }

    private static IQuestTypeDescriptor DescriptorFor(Type issueType)
    {
        var descriptor = new Mock<IQuestTypeDescriptor>();
        descriptor.Setup(mock => mock.IssueType).Returns(issueType);

        return descriptor.Object;
    }

    [Fact]
    public void IsRegistered_OnlyForTheTypesItWasBuiltWith()
    {
        var registry = new QuestTypeRegistry(new[] { DescriptorFor(typeof(FirstIssue)) });

        Assert.True(registry.IsRegistered(typeof(FirstIssue)));
        Assert.False(registry.IsRegistered(typeof(SecondIssue)));
    }

    [Fact]
    public void TryGetByName_FindsTheDescriptorByTheIssueTypeName()
    {
        var first = DescriptorFor(typeof(FirstIssue));
        var registry = new QuestTypeRegistry(new[] { first, DescriptorFor(typeof(SecondIssue)) });

        Assert.True(registry.TryGetByName(nameof(FirstIssue), out var found));
        Assert.Same(first, found);
    }

    [Fact]
    public void TryGetByName_FailsForUnknownAndNullNames()
    {
        var registry = new QuestTypeRegistry(new[] { DescriptorFor(typeof(FirstIssue)) });

        Assert.False(registry.TryGetByName(nameof(SecondIssue), out _));
        Assert.False(registry.TryGetByName(null, out _));
    }

    [Fact]
    public void Constructor_Throws_WhenTwoDescriptorsClaimTheSameIssueType()
    {
        var descriptors = new[] { DescriptorFor(typeof(FirstIssue)), DescriptorFor(typeof(FirstIssue)) };

        Assert.Throws<InvalidOperationException>(() => new QuestTypeRegistry(descriptors));
    }
}
