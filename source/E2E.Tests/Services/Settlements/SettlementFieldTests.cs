using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using HarmonyLib;
using System.Runtime.InteropServices;
using TaleWorlds.CampaignSystem;
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
            string heroId = TestEnvironment.CreateRegisteredObject<Hero>();

            var canBeClaimedIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement.CanBeClaimed)));
            var claimValueIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement.ClaimValue)));
            var claimedByIntercept = TestEnvironment.GetIntercept(AccessTools.Field(typeof(Settlement), nameof(Settlement.ClaimedBy)));


            server.Call(() =>
            {

                Assert.True(server.ObjectManager.TryGetObject<Settlement>(settlementId, out var serverSettlement));
                Assert.True(server.ObjectManager.TryGetObject<Hero>(heroId, out var serverHero));


                canBeClaimedIntercept.Invoke(null, new object[] { serverSettlement, newInt });
                claimValueIntercept.Invoke(null, new object[] { serverSettlement, newFloat });
                claimValueIntercept.Invoke(null, new object[] { serverSettlement, serverHero });

                Assert.Equal(newInt, serverSettlement.CanBeClaimed);
                Assert.Equal(newFloat, serverSettlement.ClaimValue);
                Assert.Same(serverHero, serverSettlement.ClaimedBy);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var clientSettlement));
                Assert.True(server.ObjectManager.TryGetObject<Hero>(heroId, out var clientHero));

                Assert.Equal(newFloat, clientSettlement.ClaimValue);
                Assert.Equal(newInt, clientSettlement.CanBeClaimed);
                Assert.Same(clientHero, clientSettlement.ClaimedBy);

            }
        }
    }
}
