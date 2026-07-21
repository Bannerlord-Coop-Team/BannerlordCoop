using Common;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using Coop.IntegrationTests.Kingdoms;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.Settlements.Interfaces;
using Moq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace Coop.IntegrationTests.MobileParties
{
    // Shares the serialized game-thread collection: every test that marks a game thread must be
    // serialized against the others, or two concurrently marked threads overwrite the single
    // process-wide registration.
    [Collection(KingdomSyncGameThreadCollection.Name)]
    public class EnterExitSettlementTest
    {
        internal TestEnvironment TestEnvironment { get; }

        public EnterExitSettlementTest()
        {
            TestEnvironment = new TestEnvironment();
        }

        /// <summary>
        /// The enter/exit handlers marshal the ISettlementInterface call onto the game thread. The test
        /// environment never runs a game-loop pump, so run the simulation on a thread marked as the game
        /// thread — <see cref="GameThread.Run"/> then executes inline. A dedicated thread is used, and the
        /// mark is cleared before the thread exits, so the registration never outlives the test.
        /// </summary>
        private static void RunOnGameThread(Action act)
        {
            Exception? captured = null;
            var thread = new Thread(() =>
            {
                try
                {
                    GameThread.Instance.MarkGameThread();
                    act();
                }
                catch (Exception e) { captured = e; }
                finally
                {
                    // The registration must not outlive this thread: managed thread ids are recycled,
                    // so a stale mark would make an unrelated later test thread run GameThread actions
                    // inline (observed as order-dependent CI failures in unrelated tests).
                    GameThread.Instance.UnmarkGameThread();
                }
            });
            thread.Start();
            thread.Join();
            if (captured != null) throw captured;
        }

        /// <summary>
        /// Verify sending StartSettlementEncounterAttempted on one client applies the entry through
        /// ISettlementInterface.PartyEnterSettlement on every other client.
        /// </summary>
        [Fact]
        public void EnterSettlement_AppliesViaSettlementInterface_OnOtherClients()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();

            // Register the same party/settlement on the server and all clients (consistent ids) so the
            // request resolves end-to-end and the receiving clients can apply the entry.
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            TestEnvironment.RegisterObjectInNetwork(party, "party1");
            TestEnvironment.RegisterObjectInNetwork(settlement, "settlement1");

            // Act
            RunOnGameThread(() =>
                client1.SimulateMessage(this, new StartSettlementEncounterAttempted(party, settlement)));

            // Assert
            foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
            {
                client.Resolve<Mock<ISettlementInterface>>()
                    .Verify(s => s.PartyEnterSettlement(party, settlement), Times.Once);
            }
        }

        [Fact]
        public void EnterAttempts_WhileRequestPending_SendOneRequest()
        {
            var client1 = TestEnvironment.Clients.First();
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            TestEnvironment.RegisterObjectInNetwork(party, "party1");
            TestEnvironment.RegisterObjectInNetwork(settlement, "settlement1");
            var message = new StartSettlementEncounterAttempted(party, settlement);
            var router = client1.Resolve<TestNetworkRouter>();
            router.IsMessageRoutingEnabled = false;

            RunOnGameThread(() =>
            {
                client1.SimulateMessage(this, message);
                client1.SimulateMessage(this, message);
            });

            Assert.Equal(1, client1.NetworkSentMessages.GetMessageCount<NetworkRequestStartSettlementEncounter>());
        }

        [Fact]
        public void EnterAttempt_WhileLeaveRequestPending_IsQueued()
        {
            var client1 = TestEnvironment.Clients.First();
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            TestEnvironment.RegisterObjectInNetwork(party, "party1");
            TestEnvironment.RegisterObjectInNetwork(settlement, "settlement1");
            var router = client1.Resolve<TestNetworkRouter>();
            router.IsMessageRoutingEnabled = false;

            RunOnGameThread(() =>
            {
                client1.SimulateMessage(this, new EndSettlementEncounterAttempted(party));
                client1.SimulateMessage(this, new StartSettlementEncounterAttempted(party, settlement));
                client1.SimulateMessage(this, new EndSettlementEncounterAttempted(party));
            });

            Assert.Equal(1, client1.NetworkSentMessages.GetMessageCount<NetworkRequestEndSettlementEncounter>());
            Assert.Equal(0, client1.NetworkSentMessages.GetMessageCount<NetworkRequestStartSettlementEncounter>());
        }

        [Fact]
        public void SuppressedLeave_SendsQueuedEnterRequest()
        {
            var client1 = TestEnvironment.Clients.First();
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            TestEnvironment.RegisterObjectInNetwork(party, "party1");
            TestEnvironment.RegisterObjectInNetwork(settlement, "settlement1");
            client1.Resolve<TestNetworkRouter>().IsMessageRoutingEnabled = false;

            RunOnGameThread(() =>
            {
                client1.SimulateMessage(this, new EndSettlementEncounterAttempted(party));
                client1.SimulateMessage(this, new StartSettlementEncounterAttempted(party, settlement));
                client1.SimulateMessage(
                    TestEnvironment.Server.NetPeer,
                    new NetworkSettlementEncounterLeaveResult(
                        "party1",
                        SettlementEncounterLeaveOutcome.Suppressed));
            });

            Assert.Equal(1, client1.NetworkSentMessages.GetMessageCount<NetworkRequestEndSettlementEncounter>());
            Assert.Equal(1, client1.NetworkSentMessages.GetMessageCount<NetworkRequestStartSettlementEncounter>());

            RunOnGameThread(() =>
                client1.SimulateMessage(
                    TestEnvironment.Server.NetPeer,
                    new NetworkStartSettlementEncounter(
                        new NetworkRequestStartSettlementEncounter("party1", "settlement1"))));

            client1.Resolve<Mock<ISettlementInterface>>()
                .Verify(s => s.StartSettlementEncounter(party, settlement), Times.Once);
        }

        [Fact]
        public void SuppressedLeave_AllowsNextLeaveRequest()
        {
            var client1 = TestEnvironment.Clients.First();
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            TestEnvironment.RegisterObjectInNetwork(party, "party1");
            TestEnvironment.Server.Resolve<IKingdomCreationSettlementTracker>()
                .Track("party1", "settlement1");

            RunOnGameThread(() =>
            {
                client1.SimulateMessage(this, new EndSettlementEncounterAttempted(party));
                client1.SimulateMessage(this, new EndSettlementEncounterAttempted(party));
            });

            Assert.Equal(2, client1.NetworkSentMessages.GetMessageCount<NetworkRequestEndSettlementEncounter>());
            Assert.Equal(
                2,
                TestEnvironment.Server.NetworkSentMessages
                    .GetMessages<NetworkSettlementEncounterLeaveResult>()
                    .Count(message => message.Outcome == SettlementEncounterLeaveOutcome.Suppressed));
            Assert.DoesNotContain(
                TestEnvironment.Server.NetworkSentMessages.GetMessages<NetworkSettlementEncounterLeaveResult>(),
                message => message.Outcome == SettlementEncounterLeaveOutcome.Applied);
        }

        [Fact]
        public void SuppressedLeave_AppliesDeferredApprovedEnter()
        {
            var client1 = TestEnvironment.Clients.First();
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            TestEnvironment.RegisterObjectInNetwork(party, "party1");
            TestEnvironment.RegisterObjectInNetwork(settlement, "settlement1");
            client1.Resolve<TestNetworkRouter>().IsMessageRoutingEnabled = false;

            RunOnGameThread(() =>
            {
                client1.SimulateMessage(this, new StartSettlementEncounterAttempted(party, settlement));
                client1.SimulateMessage(this, new EndSettlementEncounterAttempted(party));
                client1.SimulateMessage(
                    TestEnvironment.Server.NetPeer,
                    new NetworkStartSettlementEncounter(
                        new NetworkRequestStartSettlementEncounter("party1", "settlement1")));
            });

            client1.Resolve<Mock<ISettlementInterface>>()
                .Verify(s => s.StartSettlementEncounter(party, settlement), Times.Never);

            RunOnGameThread(() =>
                client1.SimulateMessage(
                    TestEnvironment.Server.NetPeer,
                    new NetworkSettlementEncounterLeaveResult(
                        "party1",
                        SettlementEncounterLeaveOutcome.Suppressed)));

            client1.Resolve<Mock<ISettlementInterface>>()
                .Verify(s => s.StartSettlementEncounter(party, settlement), Times.Once);
        }

        [Fact]
        public void ConfirmedLeave_DiscardsDeferredApprovedEnter()
        {
            var client1 = TestEnvironment.Clients.First();
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            TestEnvironment.RegisterObjectInNetwork(party, "party1");
            TestEnvironment.RegisterObjectInNetwork(settlement, "settlement1");
            client1.Resolve<TestNetworkRouter>().IsMessageRoutingEnabled = false;
            var settlementMock = client1.Resolve<Mock<ISettlementInterface>>();

            RunOnGameThread(() =>
            {
                client1.SimulateMessage(this, new StartSettlementEncounterAttempted(party, settlement));
                client1.SimulateMessage(this, new EndSettlementEncounterAttempted(party));
            });
            Assert.Equal(0, StartCallCount());

            RunOnGameThread(() =>
                client1.SimulateMessage(
                    TestEnvironment.Server.NetPeer,
                    new NetworkStartSettlementEncounter(
                        new NetworkRequestStartSettlementEncounter("party1", "settlement1"))));
            Assert.Equal(0, StartCallCount());

            RunOnGameThread(() =>
                client1.SimulateMessage(
                    TestEnvironment.Server.NetPeer,
                    new NetworkSettlementEncounterLeaveResult(
                        "party1",
                        SettlementEncounterLeaveOutcome.Applied)));
            Assert.Equal(0, StartCallCount());

            RunOnGameThread(() =>
                client1.SimulateMessage(
                    TestEnvironment.Server.NetPeer,
                    new NetworkSettlementEncounterLeaveResult(
                        "party1",
                        SettlementEncounterLeaveOutcome.Suppressed)));

            Assert.Equal(0, StartCallCount());

            int StartCallCount() => settlementMock.Invocations.Count(
                invocation => invocation.Method.Name == nameof(ISettlementInterface.StartSettlementEncounter));
        }

        [Fact]
        public void RejectedEnterAttempt_AllowsNextRequest()
        {
            var client1 = TestEnvironment.Clients.First();
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            TestEnvironment.RegisterObjectInNetwork(party, "party1");
            TestEnvironment.RegisterObjectInNetwork(settlement, "settlement1");
            var router = client1.Resolve<TestNetworkRouter>();
            router.IsMessageRoutingEnabled = false;

            RunOnGameThread(() =>
                client1.SimulateMessage(this, new StartSettlementEncounterAttempted(party, settlement)));

            RunOnGameThread(() =>
                client1.SimulateMessage(
                    TestEnvironment.Server.NetPeer,
                    new NetworkSettlementEncounterRejected(
                        new NetworkRequestStartSettlementEncounter("party1", "settlement1"))));

            RunOnGameThread(() =>
                client1.SimulateMessage(this, new StartSettlementEncounterAttempted(party, settlement)));

            Assert.Equal(2, client1.NetworkSentMessages.GetMessageCount<NetworkRequestStartSettlementEncounter>());
        }

        [Fact]
        public void StaleEnterResponse_DoesNotClearOrApplyPendingRequest()
        {
            var client1 = TestEnvironment.Clients.First();
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            var pendingSettlement = ObjectHelper.SkipConstructor<Settlement>();
            var staleSettlement = ObjectHelper.SkipConstructor<Settlement>();
            TestEnvironment.RegisterObjectInNetwork(party, "party1");
            TestEnvironment.RegisterObjectInNetwork(pendingSettlement, "settlement1");
            TestEnvironment.RegisterObjectInNetwork(staleSettlement, "settlement2");
            var router = client1.Resolve<TestNetworkRouter>();
            router.IsMessageRoutingEnabled = false;

            RunOnGameThread(() =>
                client1.SimulateMessage(
                    this,
                    new StartSettlementEncounterAttempted(party, pendingSettlement)));

            RunOnGameThread(() =>
                client1.SimulateMessage(
                    TestEnvironment.Server.NetPeer,
                    new NetworkStartSettlementEncounter(
                        new NetworkRequestStartSettlementEncounter("party1", "settlement2"))));
            RunOnGameThread(() =>
                client1.SimulateMessage(
                    this,
                    new StartSettlementEncounterAttempted(party, staleSettlement)));

            client1.Resolve<Mock<ISettlementInterface>>()
                .Verify(s => s.StartSettlementEncounter(It.IsAny<MobileParty>(), It.IsAny<Settlement>()), Times.Never);
            Assert.Equal(1, client1.NetworkSentMessages.GetMessageCount<NetworkRequestStartSettlementEncounter>());
        }

        [Fact]
        public void EnterSettlement_PartyAlreadyInMapEvent_IsRejectedBeforeBroadcast()
        {
            var client1 = TestEnvironment.Clients.First();
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            party.Party = ObjectHelper.SkipConstructor<PartyBase>();
            party.Party.MobileParty = party;
            party.Party._mapEventSide = ObjectHelper.SkipConstructor<MapEventSide>();
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            TestEnvironment.RegisterObjectInNetwork(party, "party1");
            TestEnvironment.RegisterObjectInNetwork(settlement, "settlement1");
            TestEnvironment.Server.NetworkSentMessages.Clear();

            RunOnGameThread(() =>
                client1.SimulateMessage(this, new StartSettlementEncounterAttempted(party, settlement)));

            TestEnvironment.Server.Resolve<Mock<ISettlementInterface>>()
                .Verify(s => s.PartyEnterSettlement(It.IsAny<MobileParty>(), It.IsAny<Settlement>()), Times.Never);
            Assert.Equal(
                1,
                TestEnvironment.Server.NetworkSentMessages.GetMessageCount<NetworkSettlementEncounterRejected>());
            Assert.Equal(0, TestEnvironment.Server.NetworkSentMessages.GetMessageCount<NetworkPartyEnterSettlement>());
        }

        [Fact]
        public void EnterSettlement_PartyAlreadyInDifferentSettlement_IsRejected()
        {
            var client1 = TestEnvironment.Clients.First();
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            var currentSettlement = ObjectHelper.SkipConstructor<Settlement>();
            var requestedSettlement = ObjectHelper.SkipConstructor<Settlement>();
            party._currentSettlement = currentSettlement;
            TestEnvironment.RegisterObjectInNetwork(party, "party1");
            TestEnvironment.RegisterObjectInNetwork(currentSettlement, "settlement1");
            TestEnvironment.RegisterObjectInNetwork(requestedSettlement, "settlement2");
            TestEnvironment.Server.NetworkSentMessages.Clear();

            RunOnGameThread(() =>
                client1.SimulateMessage(
                    this,
                    new StartSettlementEncounterAttempted(party, requestedSettlement)));

            TestEnvironment.Server.Resolve<Mock<ISettlementInterface>>()
                .Verify(s => s.PartyEnterSettlement(It.IsAny<MobileParty>(), It.IsAny<Settlement>()), Times.Never);
            Assert.Equal(
                1,
                TestEnvironment.Server.NetworkSentMessages.GetMessageCount<NetworkSettlementEncounterRejected>());
            Assert.Equal(0, TestEnvironment.Server.NetworkSentMessages.GetMessageCount<NetworkPartyEnterSettlement>());
        }

        [Fact]
        public void EnterSettlement_PartyAlreadyInRequestedSettlement_AcknowledgesWithoutReapplyingEntry()
        {
            var client1 = TestEnvironment.Clients.First();
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            party._currentSettlement = settlement;
            TestEnvironment.RegisterObjectInNetwork(party, "party1");
            TestEnvironment.RegisterObjectInNetwork(settlement, "settlement1");
            TestEnvironment.Server.NetworkSentMessages.Clear();

            RunOnGameThread(() =>
                client1.SimulateMessage(
                    this,
                    new StartSettlementEncounterAttempted(party, settlement)));

            TestEnvironment.Server.Resolve<Mock<ISettlementInterface>>()
                .Verify(s => s.PartyEnterSettlement(It.IsAny<MobileParty>(), It.IsAny<Settlement>()), Times.Never);
            Assert.Equal(1, TestEnvironment.Server.NetworkSentMessages.GetMessageCount<NetworkStartSettlementEncounter>());
            Assert.Equal(0, TestEnvironment.Server.NetworkSentMessages.GetMessageCount<NetworkPartyEnterSettlement>());
        }

        /// <summary>
        /// Starting an encounter with a besieged settlement must leave the party outside until the player
        /// chooses a valid siege action, matching vanilla's settlement-encounter flow.
        /// </summary>
        [Fact]
        public void EnterSettlement_BesiegedSettlement_DoesNotApplyEntry()
        {
            var client1 = TestEnvironment.Clients.First();
            var party = ObjectHelper.SkipConstructor<MobileParty>();
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            settlement.SiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
            TestEnvironment.RegisterObjectInNetwork(party, "party1");
            TestEnvironment.RegisterObjectInNetwork(settlement, "settlement1");

            RunOnGameThread(() =>
                client1.SimulateMessage(this, new StartSettlementEncounterAttempted(party, settlement)));

            client1.Resolve<Mock<ISettlementInterface>>()
                .Verify(s => s.StartSettlementEncounter(party, settlement), Times.Once);
            TestEnvironment.Server.Resolve<Mock<ISettlementInterface>>()
                .Verify(s => s.PartyEnterSettlement(It.IsAny<MobileParty>(), It.IsAny<Settlement>()), Times.Never);
            foreach (var client in TestEnvironment.Clients.Where(client => client != client1))
            {
                client.Resolve<Mock<ISettlementInterface>>()
                    .Verify(s => s.PartyEnterSettlement(It.IsAny<MobileParty>(), It.IsAny<Settlement>()), Times.Never);
            }
        }

        /// <summary>
        /// Verify sending EndSettlementEncounterAttempted on one client applies the exit through
        /// ISettlementInterface.PartyLeaveSettlement on every other client.
        /// </summary>
        [Fact]
        public void LeaveSettlement_AppliesViaSettlementInterface_OnOtherClients()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();

            var party = ObjectHelper.SkipConstructor<MobileParty>();
            TestEnvironment.RegisterObjectInNetwork(party, "party1");

            // Act
            RunOnGameThread(() =>
                client1.SimulateMessage(this, new EndSettlementEncounterAttempted(party)));

            // Assert
            foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
            {
                client.Resolve<Mock<ISettlementInterface>>()
                    .Verify(s => s.PartyLeaveSettlement(party), Times.Once);
            }
        }
    }
}
