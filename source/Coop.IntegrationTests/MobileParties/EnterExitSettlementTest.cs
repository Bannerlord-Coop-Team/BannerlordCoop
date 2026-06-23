using Common;
using Common.Util;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.Settlements.Interfaces;
using Moq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.IntegrationTests.MobileParties
{
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
        /// thread — <see cref="GameThread.Run"/> then executes inline. A dedicated thread is used so the
        /// marking is never left on the test-runner thread (which xUnit reuses across tests).
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

        /// <summary>
        /// While the first settlement-encounter request is still in flight, the controlled party re-attempts the
        /// encounter every campaign tick. Verify those rapid retries are rate-limited to a single network request
        /// instead of flooding the server, and the entry is applied via ISettlementInterface exactly once.
        /// </summary>
        [Fact]
        public void RapidEnterAttempts_RateLimited_ToOneRequest()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();

            var party = ObjectHelper.SkipConstructor<MobileParty>();
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            TestEnvironment.RegisterObjectInNetwork(party, "party1");
            TestEnvironment.RegisterObjectInNetwork(settlement, "settlement1");

            var message = new StartSettlementEncounterAttempted(party, settlement);

            // Act - two attempts in immediate succession, well within the request cooldown
            RunOnGameThread(() =>
            {
                client1.SimulateMessage(this, message);
                client1.SimulateMessage(this, message);
            });

            // Assert - only the first attempt reaches the server; the second is dropped by the rate limiter
            Assert.Equal(1, client1.NetworkSentMessages.GetMessageCount<NetworkRequestStartSettlementEncounter>());

            // And the entry is applied via ISettlementInterface on the other clients exactly once
            foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
            {
                client.Resolve<Mock<ISettlementInterface>>()
                    .Verify(s => s.PartyEnterSettlement(party, settlement), Times.Once);
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
