using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents
{
    public class SiegeEventPropertyTests : IDisposable
    {
        private readonly List<MethodBase> disabledMethods;
        private E2ETestEnvironment TestEnvironment { get; }
        private EnvironmentInstance Server => TestEnvironment.Server;
        private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        private readonly string siegeEventId;
        public SiegeEventPropertyTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
            disabledMethods = new();

            var SiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();

            // Create SiegeEvent on the server
            Assert.True(Server.ObjectManager.AddNewObject(SiegeEvent, out siegeEventId));

            // Create SiegeEvent on all clients
            foreach (var client in Clients)
            {
                var clientSiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
                Assert.True(client.ObjectManager.AddExisting(siegeEventId, clientSiegeEvent));
            }
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerChangeSiegeStartTime_SyncAllClients()
        {
            // Arrange
            const long value = 1337L;
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var serverSiegeEvent));

            // Act
            Server.Call(() =>
            {
                serverSiegeEvent.SiegeStartTime = new TaleWorlds.CampaignSystem.CampaignTime(value);
            });

            // Assert
            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var clientSiegeEvent));
                Assert.Equal(value, serverSiegeEvent.SiegeStartTime.NumTicks);
            }
        }

    }
}
