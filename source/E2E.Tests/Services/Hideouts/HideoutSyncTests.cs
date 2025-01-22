using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Hideouts
{
    public class HideoutSyncTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }

        EnvironmentInstance Server => TestEnvironment.Server;

        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        private readonly string HideoutId;

        public HideoutSyncTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);

            var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();

            // Create objects on the server
            Assert.True(Server.ObjectManager.AddNewObject(hideout, out HideoutId));

            // Create objects on all clients
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.AddExisting(HideoutId, hideout));
            }
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void Server_Building_Sync()
        {
            // Arrange
            var server = TestEnvironment.Server;

            var IsSpottedField = AccessTools.Field(typeof(Hideout), nameof(Hideout._isSpotted));
            var NextPossibleAttackField = AccessTools.Field(typeof(Hideout), nameof(Hideout._nextPossibleAttackTime));

            // Get field intercept to use on the server to simulate the field changing
            var IsSpottedIntercept = TestEnvironment.GetIntercept(IsSpottedField);
            var NextPossibleAttackIntercept = TestEnvironment.GetIntercept(NextPossibleAttackField);

            // Act
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<Hideout>(HideoutId, out var serverHideout));

                // Simulate the field changing
                IsSpottedIntercept.Invoke(null, new object[] { serverHideout, true });
                NextPossibleAttackIntercept.Invoke(null, new object[] { serverHideout, new CampaignTime(99) });

                serverHideout.SceneName = "testScene";

                Assert.True(serverHideout._isSpotted);
                Assert.Equal(99, serverHideout._nextPossibleAttackTime._numTicks);

                Assert.Equal("testScene", serverHideout.SceneName);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Hideout>(HideoutId, out var clientHideout));

                Assert.Equal(99, clientHideout._nextPossibleAttackTime._numTicks);
                Assert.True(clientHideout._isSpotted);

                Assert.Equal("testScene", clientHideout.SceneName);
            }
        }
    }
}
