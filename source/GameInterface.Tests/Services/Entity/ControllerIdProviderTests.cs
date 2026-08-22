using GameInterface.Services.Entity;
using System;
using Common.Network.Session;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GameInterface.Tests.Services.Entity;

public class ControllerIdProviderTests
{
    [Theory]
    [InlineData("GOG", "12345", "gog:12345")]
    [InlineData("Steam", "12345", "steam:12345")]
    public void SetControllerAsPlatformIdentity_UsesAuthenticatedProviderNamespace(
        string providerName,
        string platformUserId,
        string expected)
    {
        var store = new TestControllerIdStore("unused");
        var provider = new ControllerIdProvider(store);

        provider.SetControllerAsPlatformIdentity(new PlatformIdentity(providerName, platformUserId));

        Assert.Equal(expected, provider.ControllerId);
        Assert.Equal(platformUserId, provider.LegacyControllerId);
        Assert.Equal(0, store.CallCount);
    }

    [Theory]
    [InlineData("local", "installation-id")]
    [InlineData("epic", "account-id")]
    [InlineData("gog", "")]
    [InlineData("", "12345")]
    public void SetControllerAsPlatformIdentity_RejectsNonStorefrontOrIncompleteIdentity(
        string providerName,
        string platformUserId)
    {
        var store = new TestControllerIdStore("installation-id");
        var provider = new ControllerIdProvider(store);

        Assert.Throws<ArgumentException>(() => provider.SetControllerAsPlatformIdentity(
            new PlatformIdentity(providerName, platformUserId)));

        Assert.Null(provider.ControllerId);
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public void SetControllerAsLocalId_UsesPersistentSeparateNamespace()
    {
        var store = new TestControllerIdStore("installation-id");
        var provider = new ControllerIdProvider(store);

        provider.SetControllerAsLocalId();

        Assert.Equal("local:installation-id", provider.ControllerId);
        Assert.Equal(1, store.CallCount);
    }

    [Fact]
    public void SetControllerFromProgramArgs_WithoutDebugOverrideUsesPersistentLocalNamespace()
    {
        var store = new TestControllerIdStore("installation-id");
        var provider = new ControllerIdProvider(store);

        provider.SetControllerFromProgramArgs();

        Assert.Equal("local:installation-id", provider.ControllerId);
        Assert.Equal(1, store.CallCount);
    }

    private sealed class TestControllerIdStore : IControllerIdStore
    {
        private readonly string id;

        public TestControllerIdStore(string id)
        {
            this.id = id;
        }

        public int CallCount { get; private set; }

        public string GetOrCreateId()
        {
            CallCount++;
            return id;
        }
    }
}

public class ControllerIdStoreTests : IDisposable
{
    private readonly string tempDirectory;
    private readonly string filePath;

    public ControllerIdStoreTests()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "controller-id-tests-" + Guid.NewGuid().ToString("N"));
        filePath = Path.Combine(tempDirectory, "controller-id.txt");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void GetOrCreateId_PersistsIdentityAcrossStoreInstances()
    {
        string first = new ControllerIdStore(filePath).GetOrCreateId();
        string second = new ControllerIdStore(filePath).GetOrCreateId();

        Assert.Equal(first, second);
        Assert.True(Guid.TryParse(first, out _));
    }

    [Fact]
    public void GetOrCreateId_ReplacesMalformedIdentity()
    {
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllText(filePath, "not-an-id");

        string result = new ControllerIdStore(filePath).GetOrCreateId();

        Assert.True(Guid.TryParse(result, out _));
        Assert.Equal(result, File.ReadAllText(filePath));
    }

    [Fact]
    public async Task GetOrCreateId_ConcurrentStoresUsePersistedIdentity()
    {
        Directory.CreateDirectory(tempDirectory);
        var heldFile = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None);
        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim();
        using var blockedAttempts = new CountdownEvent(2);
        using var releaseRetry = new ManualResetEventSlim();

        Task<string> CreateId() => Task.Run(() =>
        {
            bool blockedAttemptObserved = false;
            ready.Signal();
            start.Wait();
            return new ControllerIdStore(filePath, () =>
            {
                if (!blockedAttemptObserved)
                {
                    blockedAttemptObserved = true;
                    blockedAttempts.Signal();
                }

                releaseRetry.Wait();
                Thread.Sleep(25);
            }).GetOrCreateId();
        });

        var first = CreateId();
        var second = CreateId();
        Task<string[]> combined = Task.WhenAll(first, second);

        try
        {
            ready.Wait();
            start.Set();
            var bothBlocked = Task.Run(() => blockedAttempts.Wait());
            Task completed = await Task.WhenAny(bothBlocked, combined);

            Assert.Same(bothBlocked, completed);
            Assert.False(combined.IsCompleted);
        }
        finally
        {
            heldFile.Dispose();
            releaseRetry.Set();
        }

        string[] results = await combined;

        Assert.Equal(results[0], results[1]);
        Assert.Equal(results[0], File.ReadAllText(filePath));
    }

    [Fact]
    public void GetOrCreateId_UnwritablePathDoesNotReturnTransientIdentity()
    {
        Directory.CreateDirectory(filePath);

        Assert.ThrowsAny<Exception>(() => new ControllerIdStore(filePath).GetOrCreateId());
    }
}
