using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Kingdoms
{
    public class KingdomPropertiesTest : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }

        public KingdomPropertiesTest(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void Server_KingdomProperties_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;
            uint newUint = 99;
            Banner newBanner = new Banner("151");
            CampaignTime newTime = new CampaignTime(99);

            // Act
            string? kingdomId = null;
            string? cultureId = null;
            string? settlementId = null;
            server.Call(() =>
            {
                var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
                var culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
                var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();

                Assert.True(server.ObjectManager.TryGetId(kingdom, out kingdomId));
                Assert.True(server.ObjectManager.TryGetId(culture, out cultureId));
                Assert.True(server.ObjectManager.TryGetId(settlement, out settlementId));

                kingdom.AlternativeColor = newUint;
                kingdom.AlternativeColor2 = newUint;
                kingdom.Banner = newBanner;
                kingdom.Color = newUint;
                kingdom.Color2 = newUint;
                kingdom.Culture = culture;
                kingdom.EncyclopediaRulerTitle = new TextObject("rulerTitle");
                kingdom.EncyclopediaText = new TextObject("text");
                kingdom.EncyclopediaTitle = new TextObject("title");
                kingdom.InformalName = new TextObject("name");
                kingdom.InitialHomeLand = settlement;
                kingdom.LabelColor = newUint;
                kingdom.LastArmyCreationDay = 99;
                kingdom.LastKingdomDecisionConclusionDate = newTime;
                kingdom.LastMercenaryOfferTime = newTime;
                kingdom.MainHeroCrimeRating = newUint;
                kingdom.MercenaryWallet = 99;
                kingdom.Name = new TextObject("name");
                kingdom.NotAttackableByPlayerUntilTime = newTime;
                kingdom.PrimaryBannerColor = newUint;
                kingdom.SecondaryBannerColor = newUint;
            });

            // Assert
            Assert.NotNull(kingdomId);
            Assert.NotNull(cultureId);
            Assert.NotNull(settlementId);

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var clientKingdom));
                Assert.True(client.ObjectManager.TryGetObject<CultureObject>(cultureId, out var clientCulture));
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var clientSettlement));

                Assert.Equal(newUint, clientKingdom.AlternativeColor);
                Assert.Equal(newUint, clientKingdom.AlternativeColor2);
                Assert.Equal(newBanner.Serialize(), clientKingdom.Banner.Serialize());
                Assert.Equal(newUint, clientKingdom.Color);
                Assert.Equal(newUint, clientKingdom.Color2);
                Assert.Equal(clientCulture, clientKingdom.Culture);
                Assert.Equal("rulerTitle", clientKingdom.EncyclopediaRulerTitle.ToString());
                Assert.Equal("text", clientKingdom.EncyclopediaText.ToString());
                Assert.Equal("title", clientKingdom.EncyclopediaTitle.ToString());
                Assert.Equal("name", clientKingdom.InformalName.ToString());
                Assert.Equal(clientSettlement, clientKingdom.InitialHomeLand);
                Assert.Equal(newUint, clientKingdom.LabelColor);
                Assert.Equal(99, clientKingdom.LastArmyCreationDay);
                Assert.Equal(newTime, clientKingdom.LastKingdomDecisionConclusionDate);
                Assert.Equal(newTime, clientKingdom.LastMercenaryOfferTime);
                Assert.Equal(newUint, clientKingdom.MainHeroCrimeRating);
                Assert.Equal(99, clientKingdom.MercenaryWallet);
                Assert.Equal("name", clientKingdom.Name.ToString());
                Assert.Equal(newTime, clientKingdom.NotAttackableByPlayerUntilTime);
                Assert.Equal(newUint, clientKingdom.PrimaryBannerColor);
                Assert.Equal(newUint, clientKingdom.SecondaryBannerColor);
            }
        }
    }
}