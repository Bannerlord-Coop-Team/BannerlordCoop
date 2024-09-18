using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Settlements
{
    public class SettlementFieldTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }

        EnvironmentInstance Server => TestEnvironment.Server;

        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        private string SettlementId;

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



            server.Call(() =>
            {
                var settlement = new Settlement();

                Assert.True(server.ObjectManager.TryGetId(settlement, out SettlementId));

                settlement.CanBeClaimed = newInt;
                settlement.ClaimValue = newFloat;

                Assert.Equal(newInt, settlement.CanBeClaimed);
                Assert.Equal(newFloat, settlement.ClaimValue);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(SettlementId, out var settlement));

                Assert.Equal(newFloat, settlement.ClaimValue);
                Assert.Equal(newInt, settlement.CanBeClaimed);

            }
        }
    }
}
