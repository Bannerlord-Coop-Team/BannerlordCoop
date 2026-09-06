using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class MapEventInitializationBarrierTests
{
    [Fact]
    public void Binding_IsIsolatedByScopeAndRemovedOnDisposal()
    {
        var firstBarrier = new Mock<IMapEventInitializationBarrier>().Object;
        var secondBarrier = new Mock<IMapEventInitializationBarrier>().Object;
        var firstScope = BuildBindingScope(firstBarrier);
        using var secondScope = BuildBindingScope(secondBarrier);

        try
        {
            Assert.True(MapEventInitializationBarrierBinding.TryGet(firstScope, out var first));
            Assert.Same(firstBarrier, first);
            Assert.True(MapEventInitializationBarrierBinding.TryGet(secondScope, out var second));
            Assert.Same(secondBarrier, second);

            firstScope.Dispose();

            Assert.False(MapEventInitializationBarrierBinding.TryGet(firstScope, out _));
            Assert.True(MapEventInitializationBarrierBinding.TryGet(secondScope, out second));
            Assert.Same(secondBarrier, second);
        }
        finally
        {
            firstScope.Dispose();
        }
    }

    [Fact]
    public void PendingPartyView_TracksCancellationAbortAndDisposal()
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        var party = ObjectHelper.SkipConstructor<PartyBase>();
        var barrier = CreateBarrier(mapEvent, party);

        barrier.Register(mapEvent);
        barrier.SetServerPartyPending(mapEvent, party, pending: true);
        Assert.True(barrier.IsPartyPending(party));

        barrier.SetServerPartyPending(mapEvent, party, pending: false);
        Assert.False(barrier.IsPartyPending(party));

        barrier.SetServerPartyPending(mapEvent, party, pending: true);
        barrier.AbortServer(mapEvent);
        Assert.False(barrier.IsPartyPending(party));

        barrier.Register(mapEvent);
        barrier.SetServerPartyPending(mapEvent, party, pending: true);
        barrier.Dispose();
        Assert.False(barrier.IsPartyPending(party));
    }

    [Fact]
    public async Task PendingPartyView_SupportsConcurrentReadersWhilePublishing()
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        var party = ObjectHelper.SkipConstructor<PartyBase>();
        var unrelatedParty = ObjectHelper.SkipConstructor<PartyBase>();
        using var barrier = CreateBarrier(mapEvent, party);
        barrier.Register(mapEvent);

        using var cancellation = new CancellationTokenSource();
        using var readersReady = new CountdownEvent(4);
        using var startReaders = new ManualResetEventSlim();
        var failures = new ConcurrentQueue<Exception>();
        long readCount = 0;
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            readersReady.Signal();
            startReaders.Wait();
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    barrier.IsPartyPending(party);
                    if (barrier.IsPartyPending(unrelatedParty))
                        throw new InvalidOperationException("An unrelated party appeared in the pending snapshot");
                    Interlocked.Increment(ref readCount);
                }
                catch (Exception ex)
                {
                    failures.Enqueue(ex);
                    return;
                }
            }
        })).ToArray();

        readersReady.Wait();
        startReaders.Set();
        for (int i = 0; i < 1000; i++)
        {
            barrier.SetServerPartyPending(mapEvent, party, pending: true);
            barrier.SetServerPartyPending(mapEvent, party, pending: false);
        }
        cancellation.Cancel();
        await Task.WhenAll(readers);

        Assert.True(Interlocked.Read(ref readCount) > 0);
        Assert.Empty(failures);
        Assert.False(barrier.IsPartyPending(party));
    }

    private static IContainer BuildBindingScope(IMapEventInitializationBarrier barrier)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(barrier).As<IMapEventInitializationBarrier>();
        builder.RegisterType<MapEventInitializationBarrierBinding>().InstancePerLifetimeScope().AutoActivate();
        return builder.Build();
    }

    private static MapEventInitializationBarrier CreateBarrier(MapEvent mapEvent, PartyBase party)
    {
        var objectManager = new Mock<IObjectManager>();
        string mapEventId = "map-event";
        string partyId = "party";
        objectManager.Setup(manager => manager.TryGetIdWithLogging(mapEvent, out mapEventId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(party, out partyId)).Returns(true);

        return new MapEventInitializationBarrier(
            new Mock<IMessageBroker>().Object,
            new Mock<INetwork>().Object,
            objectManager.Object,
            new StubSiegeEventGraphSynchronizer());
    }

    private sealed class StubSiegeEventGraphSynchronizer : ISiegeEventGraphSynchronizer
    {
        public bool TryCapture(
            SiegeEvent siegeEvent,
            out SiegeEventGraphSnapshot snapshot,
            MobileParty fallbackLeaderParty = null)
        {
            snapshot = default;
            return false;
        }

        public bool TryApply(SiegeEventGraphSnapshot snapshot) => false;
    }
}
