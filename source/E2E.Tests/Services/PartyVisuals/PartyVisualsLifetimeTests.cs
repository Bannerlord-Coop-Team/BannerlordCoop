using E2E.Tests.Environment;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyVisuals
{
    public class PartyVisualsLifetimeTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }
        public PartyVisualsLifetimeTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerCreatePartyVisual_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? visualId = null;
            server.Call(() =>
            {
                var MobileParty = new MobileParty();
                var partyBase = new PartyBase(MobileParty);
                var partyVisual = new PartyVisual(partyBase);

                Assert.True(server.ObjectManager.TryGetId(partyVisual, out visualId));
            });

            // Assert
            Assert.NotNull(visualId);

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<PartyVisual>(visualId, out var _));
            }
        }

        //[Fact]
        //public void ClientCreatePartyVisual_DoesNothing()
        //{
        //    // Arrange
        //    var client1 = TestEnvironment.Clients.First();

        //    // Act
        //    string? KingdomId = null;
        //    client1.Call(() =>
        //    {
        //        var Kingdom = new Kingdom();

        //        Assert.False(client1.ObjectManager.TryGetId(Kingdom, out KingdomId));
        //    });

        //    // Assert
        //    Assert.Null(KingdomId);
        //}
    }
}
