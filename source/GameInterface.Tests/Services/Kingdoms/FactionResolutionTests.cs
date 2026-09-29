using Common;
using Common.Messaging;
using Coop.Tests.Mocks;
using GameInterface.Services.Kingdoms.Interfaces;
using GameInterface.Services.Stances.Handlers;
using GameInterface.Services.Stances.Messages;
using Moq;
using Serilog;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

[Collection(ModInformationRoleCollection.Name)]
public class FactionResolutionTests
{
    static FactionResolutionTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Resolve_ClanOrKingdom_FullOrCompactId_WithoutWrongTypeLookup(bool kingdom, bool compact)
    {
        var logger = new Mock<ILogger>();
        var manager = new global::GameInterface.Services.ObjectManager.ObjectManager(logger.Object);
        IFaction faction = (IFaction)FormatterServices.GetUninitializedObject(kingdom ? typeof(Kingdom) : typeof(Clan));
        string fullId = kingdom ? "Kingdom_test" : "Clan_test";
        Assert.True(manager.AddExisting(fullId, faction));

        Assert.True(new FactionInterface(manager).TryGetFaction(compact ? "test" : fullId, out var resolved));

        Assert.Same(faction, resolved);
        Assert.DoesNotContain(logger.Invocations, call => call.Method.Name == "Error");
    }

    [Fact]
    public void Resolve_CompactId_PreservesKingdomPrecedenceOverBareAndClanIds()
    {
        var manager = new global::GameInterface.Services.ObjectManager.ObjectManager(new Mock<ILogger>().Object);
        var kingdom = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));
        var clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
        var bareClan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
        Assert.True(manager.AddExisting("Kingdom_test", kingdom));
        Assert.True(manager.AddExisting("Clan_test", clan));
        Assert.True(manager.AddExisting("test", bareClan));
        var resolver = new FactionInterface(manager);

        Assert.True(resolver.TryGetFaction("test", out var resolved));
        Assert.Same(kingdom, resolved);
        Assert.True(manager.Remove(kingdom));
        Assert.True(resolver.TryGetFaction("test", out resolved));
        Assert.Same(clan, resolved);
        Assert.True(manager.Remove(clan));
        Assert.True(resolver.TryGetFaction("test", out resolved));
        Assert.Same(bareClan, resolved);
        Assert.True(manager.Remove(bareClan));
        Assert.True(manager.AddExisting("test", kingdom));
        Assert.True(manager.AddExisting("Clan_test", clan));
        Assert.True(resolver.TryGetFaction("test", out resolved));
        Assert.Same(kingdom, resolved);
    }

    [Fact]
    public void Resolve_MissingOrWrongType_DoesNotHideWrongTypeDiagnostic()
    {
        var logger = new Mock<ILogger>();
        var manager = new global::GameInterface.Services.ObjectManager.ObjectManager(logger.Object);
        var resolver = new FactionInterface(manager);
        Assert.False(resolver.TryGetFaction("missing", out _));
        Assert.True(manager.AddExisting("Clan_wrong", new object()));
        Assert.False(resolver.TryGetFaction("Clan_wrong", out _));
        Assert.Contains(logger.Invocations, call => call.Method.Name == "Error");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StanceReceive_ResolvesAfterEarlierQueuedRegistration(bool peace)
    {
        Assert.True(GameThread.Instance.IsInitialized);
        Assert.False(GameThread.Instance.IsGameThread);
        using var broker = new MessageBroker();
        var faction = (IFaction)FormatterServices.GetUninitializedObject(typeof(Clan));
        var resolver = new Mock<IFactionInterface>();
        bool registered = false;
        bool resolvedOnGameThread = false;
        bool registrationSeen = false;
        int calls = 0;
        resolver.Setup(r => r.TryGetFaction("Clan_pending", out faction))
            .Callback(() =>
            {
                calls++;
                resolvedOnGameThread = GameThread.Instance.IsGameThread;
                registrationSeen = registered;
            }).Returns(true);
        // The second ID is absent, so no vanilla action should run.
        using var handler = new FactionStanceHandler(broker, resolver.Object);
        var blocked = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);
        try
        {
            GameThread.RunSafe(() =>
            {
                blocked.Set();
                release.Wait(TimeSpan.FromSeconds(30));
            });
            Assert.True(blocked.Wait(TimeSpan.FromSeconds(10)));
            GameThread.RunSafe(() => registered = true);
            if (peace)
                broker.Publish(this, new MakePeaceChanged("Clan_pending", "missing", 0, 0, 0));
            else
                broker.Publish(this, new DeclareWarChanged("Clan_pending", "missing", 0));
            Assert.Equal(0, calls);
        }
        finally
        {
            release.Set();
        }
        GameThread.Run(() => { }, blocking: true);
        Assert.Equal(1, calls);
        Assert.True(resolvedOnGameThread);
        Assert.True(registrationSeen);
    }
}
