using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using HarmonyLib;
using System.Runtime.InteropServices;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Settlements
{
    public class SettlementFieldTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }

        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        public SettlementFieldTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);


        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void Server_Settlement_Fields()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            const float newFloat = 540;
            const int newInt = 5;

            string settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

            var field = AccessTools.Field(typeof(Settlement), nameof(Settlement.CanBeClaimed));
            
            var canBeClaimedIntercept = TestEnvironment.GetIntercept(field);
            
            server.Call(() =>
            {

                Assert.True(server.ObjectManager.TryGetObject<Settlement>(settlementId, out var serverSettlement));

                canBeClaimedIntercept.Invoke(null, new object[] { serverSettlement, newInt });
                //claimValueIntercept.Invoke(null, new object[] { serverSettlement, newFloat });

                Assert.Equal(newInt, serverSettlement.CanBeClaimed);
                Assert.Equal(newFloat, serverSettlement.ClaimValue);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var clientSettlement));

                Assert.Equal(newFloat, clientSettlement.ClaimValue);
                Assert.Equal(newInt, clientSettlement.CanBeClaimed);

            }
        }
    }
}
