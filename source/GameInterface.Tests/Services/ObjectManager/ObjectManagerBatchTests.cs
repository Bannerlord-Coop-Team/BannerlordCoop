using GameInterface.Services.ObjectManager;
using Moq;
using Serilog;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.ObjectManagement;

public class ObjectManagerBatchTests
{
    private sealed class RegisteredObject
    {
    }

    private static IObjectManager CreateObjectManager() =>
        new global::GameInterface.Services.ObjectManager.ObjectManager(Mock.Of<ILogger>());

    [Fact]
    public void AddExistingBatch_RegistersEveryForwardAndReverseLookup()
    {
        var objectManager = CreateObjectManager();
        var first = new RegisteredObject();
        var second = new RegisteredObject();

        Assert.True(objectManager.AddExistingBatch(new[]
        {
            new KeyValuePair<string, object>("RegisteredObject_first", first),
            new KeyValuePair<string, object>("RegisteredObject_second", second),
        }));

        Assert.True(objectManager.TryGetObject<RegisteredObject>("first", out var resolvedFirst));
        Assert.True(objectManager.TryGetObject<RegisteredObject>("second", out var resolvedSecond));
        Assert.Same(first, resolvedFirst);
        Assert.Same(second, resolvedSecond);

        Assert.True(objectManager.TryGetId(first, out var firstId));
        Assert.True(objectManager.TryGetId(second, out var secondId));
        Assert.Equal("RegisteredObject_first", firstId);
        Assert.Equal("RegisteredObject_second", secondId);
    }

    [Fact]
    public void AddExistingBatch_RepeatedIdLeavesEveryCandidateUnregistered()
    {
        var objectManager = CreateObjectManager();
        var first = new RegisteredObject();
        var second = new RegisteredObject();

        Assert.False(objectManager.AddExistingBatch(new[]
        {
            new KeyValuePair<string, object>("RegisteredObject_duplicate", first),
            new KeyValuePair<string, object>("RegisteredObject_duplicate", second),
        }));

        Assert.False(objectManager.Contains("RegisteredObject_duplicate"));
        Assert.False(objectManager.Contains(first));
        Assert.False(objectManager.Contains(second));
    }

    [Fact]
    public void AddExistingBatch_ExistingCollisionLeavesEarlierCandidateUnregistered()
    {
        var objectManager = CreateObjectManager();
        var existing = new RegisteredObject();
        var validCandidate = new RegisteredObject();
        var collidingCandidate = new RegisteredObject();
        Assert.True(objectManager.AddExisting("RegisteredObject_existing", existing));

        Assert.False(objectManager.AddExistingBatch(new[]
        {
            new KeyValuePair<string, object>("RegisteredObject_valid", validCandidate),
            new KeyValuePair<string, object>("RegisteredObject_existing", collidingCandidate),
        }));

        Assert.False(objectManager.Contains("RegisteredObject_valid"));
        Assert.False(objectManager.Contains(validCandidate));
        Assert.False(objectManager.Contains(collidingCandidate));
        Assert.True(objectManager.TryGetObject<RegisteredObject>("existing", out var resolvedExisting));
        Assert.Same(existing, resolvedExisting);
    }

    [Fact]
    public void RemoveExistingBatch_RemovesAllPresentMappingsAndIgnoresMissingObjects()
    {
        var objectManager = CreateObjectManager();
        var first = new RegisteredObject();
        var second = new RegisteredObject();
        var absent = new RegisteredObject();
        Assert.True(objectManager.AddExistingBatch(new[]
        {
            new KeyValuePair<string, object>("RegisteredObject_first", first),
            new KeyValuePair<string, object>("RegisteredObject_second", second),
        }));

        Assert.True(objectManager.RemoveExistingBatch(new object[] { first, absent, second, first }));

        Assert.False(objectManager.Contains(first));
        Assert.False(objectManager.Contains(second));
        Assert.False(objectManager.Contains("RegisteredObject_first"));
        Assert.False(objectManager.Contains("RegisteredObject_second"));
    }
}
