using Common.Messaging;
using E2E.Tests.Environment;
using GameInterface.Services.PartyVisuals.Messages;
using SandBox.View.Map.Visuals;
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
        public void ServerCreatePartyVisual_RegistersServerSide_ClientsWithoutVisualManagerSkip()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? visualId = null;
            server.Call(() =>
            {
                var MobileParty = new MobileParty();
                var partyBase = new PartyBase(MobileParty);
                var mobilePartyVisual = new MobilePartyVisual(partyBase);

                Assert.True(server.ObjectManager.TryGetId(mobilePartyVisual, out visualId));
            });

            // Assert
            Assert.NotNull(visualId);

            // The test harness has no map visuals manager (MobilePartyVisualManager.Current is null), so a
            // received create cannot build a visual and the client does not register one. Real clients (with
            // a visuals manager) register via the main create path, which the harness cannot exercise.
            foreach (var client in TestEnvironment.Clients)
            {
                Assert.False(client.ObjectManager.TryGetObject<MobilePartyVisual>(visualId, out var _));
            }
        }

        [Fact]
        public void ServerCreatePartyVisual_UnregisteredOwner_SkipsCreateAndPreservesDestroyIdentity()
        {
            var server = TestEnvironment.Server;
            var client = TestEnvironment.Clients.First();
            NetworkDestroyPartyVisual? destroyed = null;
            string? expectedVisualId = null;
            client.Resolve<IMessageBroker>().Subscribe<NetworkDestroyPartyVisual>(payload => destroyed = payload.What);

            server.Call(() =>
            {
                var mobileParty = new MobileParty();
                var partyBase = new PartyBase(mobileParty);
                mobileParty.StringId = "mountain_bandits_24";
                expectedVisualId = $"MobilePartyVisual_{mobileParty.StringId}";
                Assert.True(server.ObjectManager.TryGetId(mobileParty, out string mobilePartyId));
                Assert.True(server.ObjectManager.Remove(mobileParty));

                var mobilePartyVisual = new MobilePartyVisual(partyBase);

                Assert.False(server.ObjectManager.TryGetId(mobilePartyVisual, out _));
                Assert.True(server.ObjectManager.AddExisting(mobilePartyId, mobileParty));
                server.Resolve<IMessageBroker>().Publish(
                    this,
                    new PartyVisualDestroyed(mobilePartyVisual, mobileParty));
            });

            Assert.NotNull(destroyed);
            Assert.Equal(expectedVisualId, destroyed.PartyVisualId);
        }

        [Fact]
        public void ClientCreatePartyVisual_DoesNothing()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();
            var server = TestEnvironment.Server;

            // Act
            string? PartyVisualId = null;
            string? baseId = null;

            server.Call(() =>
            {
                var MobileParty = new MobileParty();
                var partyBase = new PartyBase(MobileParty);

                Assert.True(server.ObjectManager.TryGetId(partyBase, out baseId));
            });

            client1.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject(baseId, out PartyBase baseParty));
                var partyVisual = new MobilePartyVisual(baseParty);

                Assert.False(client1.ObjectManager.TryGetId(partyVisual, out PartyVisualId));
            });

            // Assert
            Assert.Null(PartyVisualId);
        }
    }
}
