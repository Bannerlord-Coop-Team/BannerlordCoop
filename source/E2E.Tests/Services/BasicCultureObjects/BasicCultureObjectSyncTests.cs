using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BasicCultureObjects
{
    public class BasicCultureObjectSyncTests
    {
        E2ETestEnvironment TestEnvironment { get; }

        EnvironmentInstance Server => TestEnvironment.Server;

        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        public BasicCultureObjectSyncTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        [Fact]
        public void ServerBasicCultureObject_SyncAll()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? cultureId = null;
            server.Call(() =>
            {
                BasicCultureObject culture = new BasicCultureObject();

                Assert.True(server.ObjectManager.TryGetId(culture, out cultureId));

                culture.BackgroundColor1 = 33U;
                culture.BackgroundColor2 = 33U;
                culture.BannerKey = "testBanner";
                culture.CanHaveSettlement = true;
                culture.ClothAlternativeColor = 33U;
                culture.ClothAlternativeColor2 = 33U;
                culture.Color = 33U;
                culture.Color2 = 33U;
                culture.EncounterBackgroundMesh = "testMesh";
                culture.ForegroundColor1 = 33U;
                culture.ForegroundColor2 = 33U;
                culture.IsBandit = true;
                culture.IsMainCulture = true;
                culture.Name = new TextObject("testName");
            });

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(cultureId, out BasicCultureObject clientCulture));

                Assert.Equal(33U, clientCulture.BackgroundColor1);
                Assert.Equal(33U, clientCulture.BackgroundColor2);
                Assert.Equal("testBanner", clientCulture.BannerKey);
                Assert.True(clientCulture.CanHaveSettlement);
                Assert.Equal(33U, clientCulture.ClothAlternativeColor);
                Assert.Equal(33U, clientCulture.ClothAlternativeColor2);
                Assert.Equal(33U, clientCulture.Color);
                Assert.Equal(33U, clientCulture.Color2);
                Assert.Equal("testMesh", clientCulture.EncounterBackgroundMesh);
                Assert.Equal(33U, clientCulture.ForegroundColor1);
                Assert.Equal(33U, clientCulture.ForegroundColor2);
                Assert.True(clientCulture.IsBandit);
                Assert.True(clientCulture.IsMainCulture);
                Assert.Equal("testName", clientCulture.Name.Value);
            }
        }
    }
}
