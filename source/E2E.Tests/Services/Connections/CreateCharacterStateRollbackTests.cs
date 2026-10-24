using Common;
using Common.Messaging;
using Common.Network;
using Common.Tests.Utils;
using Common.Util;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using E2E.Tests.Environment.Extensions;
using GameInterface.Services.Heroes.Data;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace E2E.Tests.Services.Connections;

/// <summary>
/// Verifies a failed character creation still rolls the new player back once and drops the joiner when the
/// server's blocking rollback times out. Lives in E2E because it needs an isolated game-thread queue.
/// </summary>
public class CreateCharacterStateRollbackTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(10);
    private static readonly string[] RegistrationIds = { "Hero_test", "MobileParty_test" };

    [Fact]
    public void HandleCaptureFailure_WhoseRollbackTimesOutBeforeItStarts_RollsBackOnceThenDisconnects()
    {
        using var fixture = new Fixture(handleCaptureFails: true);
        using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);

        // Nothing pumps while the poller waits, so the blocking rollback expires before it starts.
        Assert.Null(fixture.TransferHeroOnWorker().Join());
        fixture.Rollback.Verify(value => value.Rollback(It.IsAny<Player>(), It.IsAny<string[]>()), Times.Never);
        Assert.NotEqual(ConnectionState.ShutdownRequested, fixture.Peer.ConnectionState);

        fixture.Pump();
        fixture.Pump();

        fixture.Rollback.Verify(value => value.Rollback(It.IsAny<Player>(), RegistrationIds), Times.Once);
        Assert.Equal(ConnectionState.ShutdownRequested, fixture.Peer.ConnectionState);
        Assert.Empty(fixture.SentToOthers);
        Assert.Equal(0, fixture.Queue.Count);
    }

    [Fact]
    public void HandleCaptureFailure_WhoseRollbackTimesOutWhileItRuns_RollsBackOnceThenDisconnects()
    {
        using var fixture = new Fixture(handleCaptureFails: true);
        using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        Transfer? transfer = null;
        bool pollerReturnedDuringRollback = false;
        fixture.DuringRollback = () => pollerReturnedDuringRollback = transfer!.TryJoin(LongTimeout);
        transfer = fixture.TransferHeroOnWorker();
        fixture.WaitUntilQueued();

        // The rollback starts and holds the game thread until the poller gave up on both waits.
        fixture.Pump();
        fixture.Pump();

        Assert.True(pollerReturnedDuringRollback, "the poller did not time out while the rollback ran");
        Assert.Null(transfer.Join());
        fixture.Rollback.Verify(value => value.Rollback(It.IsAny<Player>(), RegistrationIds), Times.Once);
        Assert.Equal(ConnectionState.ShutdownRequested, fixture.Peer.ConnectionState);
        Assert.Empty(fixture.SentToOthers);
        Assert.Equal(0, fixture.Queue.Count);
    }

    [Fact]
    public void SetupFailure_WhoseRollbackTimesOutBeforeItStarts_RollsBackOnceThenTellsTheOthersAndDisconnects()
    {
        using var fixture = new Fixture(handleCaptureFails: false);
        using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), ShortTimeout);

        Assert.Null(fixture.TransferHeroOnWorker().Join());
        fixture.Rollback.Verify(value => value.Rollback(It.IsAny<Player>(), It.IsAny<string[]>()), Times.Never);
        Assert.NotEqual(ConnectionState.ShutdownRequested, fixture.Peer.ConnectionState);

        fixture.Pump();
        fixture.Pump();

        fixture.AssertRolledBackOnceThenDisconnected();
    }

    [Fact]
    public void SetupFailure_WhoseRollbackTimesOutWhileItRuns_RollsBackOnceThenTellsTheOthersAndDisconnects()
    {
        using var fixture = new Fixture(handleCaptureFails: false);
        using var shortTimeout = GameThread.Instance.LimitFrameDrain(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        Transfer? transfer = null;
        bool pollerReturnedDuringRollback = false;
        fixture.DuringRollback = () => pollerReturnedDuringRollback = transfer!.TryJoin(LongTimeout);
        transfer = fixture.TransferHeroOnWorker();
        fixture.WaitUntilQueued();

        // The rollback starts and holds the game thread until the poller gave up on both waits.
        fixture.Pump();
        fixture.Pump();

        Assert.True(pollerReturnedDuringRollback, "the poller did not time out while the rollback ran");
        Assert.Null(transfer.Join());
        fixture.AssertRolledBackOnceThenDisconnected();
    }

    /// <summary>A server-side character creation for one joining peer, on its own game-thread queue.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly int previousGameThreadId = GameThread.Instance.GameThreadId;
        private readonly IDisposable queueScope;
        private readonly CancellationTokenSource session = new();
        private readonly CreateCharacterState state;
        private readonly Mock<IPlayerManager> playerManager = new();
        private readonly List<IMessage> sentToOthers = new();
        private Player? createdPlayer;

        public GameThread.QueueContext Queue { get; } = new();
        public NetPeer Peer { get; } = NetPeerExtensions.CreatePeer(1);
        public Mock<IPlayerCreationRollback> Rollback { get; } = new();
        public Action? DuringRollback { get; set; }

        public IReadOnlyList<IMessage> SentToOthers
        {
            get
            {
                lock (sentToOthers) return sentToOthers.ToArray();
            }
        }

        public Fixture(bool handleCaptureFails)
        {
            queueScope = GameThread.ActivateQueue(Queue);
            GameThread.Instance.MarkGameThread();

            var hero = ObjectHelper.SkipConstructor<Hero>();
            hero._partyBelongedTo = ObjectHelper.SkipConstructor<MobileParty>();
            hero._clan = ObjectHelper.SkipConstructor<Clan>();
            hero._characterObject = ObjectHelper.SkipConstructor<CharacterObject>();

            var objectManager = new Mock<IObjectManager>();
            string heroId = "Hero_test", partyId = "MobileParty_test", clanId = "Clan_test", characterId = "CharacterObject_test";
            objectManager.Setup(value => value.TryGetIdWithLogging(It.IsAny<Hero>(), out heroId)).Returns(true);
            objectManager.Setup(value => value.TryGetIdWithLogging(It.IsAny<MobileParty>(), out partyId)).Returns(true);
            objectManager.Setup(value => value.TryGetIdWithLogging(It.IsAny<Clan>(), out clanId)).Returns(true);
            objectManager.Setup(value => value.TryGetIdWithLogging(It.IsAny<CharacterObject>(), out characterId)).Returns(true);

            var heroInterface = new Mock<IHeroInterface>();
            heroInterface.Setup(value => value.ServerUnpackHero(It.IsAny<byte[]>())).Returns(hero);
            var handles = new PlayerRegistrationHandles(1, 2, 3, 4, 5, 6, 7, 8, 9);
            heroInterface.Setup(value => value.TryGetRegistrationHandles(hero, out handles)).Returns(!handleCaptureFails);
            heroInterface.Setup(value => value.SetupServerHero(hero)).Throws(new InvalidOperationException("setup failed"));

            playerManager.Setup(value => value.AddPlayer(It.IsAny<Player>()))
                .Callback<Player>(player => createdPlayer = player)
                .Returns(true);
            playerManager.Setup(value => value.RemovePlayer(It.IsAny<Player>())).Returns(true);
            Rollback.Setup(value => value.CaptureRegistrationIds(It.IsAny<Player>())).Returns(RegistrationIds);
            Rollback.Setup(value => value.Rollback(It.IsAny<Player>(), It.IsAny<string[]>()))
                .Callback(() => DuringRollback?.Invoke());

            var network = new Mock<INetwork>();
            network.Setup(value => value.SendAllBut(It.IsAny<NetPeer>(), It.IsAny<IMessage>()))
                .Callback<NetPeer, IMessage>((_, message) =>
                {
                    lock (sentToOthers) sentToOthers.Add(message);
                });

            var connectionLogic = new Mock<IConnectionLogic>();
            connectionLogic.SetupGet(value => value.Peer).Returns(Peer);
            state = new CreateCharacterState(
                connectionLogic.Object,
                objectManager.Object,
                new TestMessageBroker(),
                network.Object,
                heroInterface.Object,
                playerManager.Object,
                Rollback.Object,
                Mock.Of<IExistingPlayerSender>());
        }

        /// <summary>Delivers the joiner's hero on a worker inside the session, as the network poller does.</summary>
        public Transfer TransferHeroOnWorker() => new(() =>
        {
            using (GameThread.ActivateCancellation(session.Token))
            {
                state.Handle_NetworkTransferNewHero(new MessagePayload<NetworkTransferNewHero>(
                    Peer, new NetworkTransferNewHero("MyId", Array.Empty<byte>())));
            }
        });

        public void WaitUntilQueued() =>
            Assert.True(SpinWait.SpinUntil(() => Queue.Count > 0, LongTimeout), "the rollback was never queued");

        public void Pump() => GameThread.Instance.Update(TimeSpan.Zero);

        public void AssertRolledBackOnceThenDisconnected()
        {
            Assert.NotNull(createdPlayer);
            playerManager.Verify(value => value.RemovePlayer(createdPlayer!), Times.Once);
            Rollback.Verify(value => value.Rollback(createdPlayer!, RegistrationIds), Times.Once);
            Assert.Equal(ConnectionState.ShutdownRequested, Peer.ConnectionState);
            Assert.Collection(
                SentToOthers,
                message => Assert.IsType<NetworkNewPlayerHeroCreated>(message),
                message =>
                {
                    var rolledBack = Assert.IsType<NetworkPlayerCreationRolledBack>(message);
                    Assert.Same(createdPlayer, rolledBack.Player);
                    Assert.Equal(RegistrationIds, rolledBack.RegistrationIds);
                });
            Assert.Equal(0, Queue.Count);
        }

        public void Dispose()
        {
            try
            {
                state.Dispose();
                GameThread.Instance.DiscardQueuedActions(Queue);
            }
            finally
            {
                queueScope.Dispose();
                GameThread.Instance.RestoreGameThread(previousGameThreadId);
                session.Dispose();
            }
        }
    }

    /// <summary>
    /// The hero transfer on its own thread. Task.Run is avoided because a waiting test thread, which is the marked
    /// game thread, may run the task inline.
    /// </summary>
    private sealed class Transfer
    {
        private readonly Thread thread;
        private Exception? failure;

        public Transfer(Action deliver)
        {
            thread = new Thread(() =>
            {
                try
                {
                    deliver();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            }) { IsBackground = true };
            thread.Start();
        }

        public bool TryJoin(TimeSpan timeout) => thread.Join(timeout);

        public Exception? Join()
        {
            Assert.True(thread.Join(LongTimeout), "the hero transfer did not return");
            return failure;
        }
    }
}
