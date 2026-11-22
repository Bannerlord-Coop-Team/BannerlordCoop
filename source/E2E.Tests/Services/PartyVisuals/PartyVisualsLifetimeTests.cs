using Common.Messaging;
using E2E.Tests.Environment;
using GameInterface.Services.PartyVisuals.Interfaces;
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
        public void HeadlessLifetime_UsesPartyHandleWithoutRegisteringVisualObjects()
        {
            var server = TestEnvironment.Server;
            var client = TestEnvironment.Clients.First();
            var created = new List<NetworkCreatePartyVisual>();
            var destroyed = new List<NetworkDestroyPartyVisual>();
            client.Resolve<IMessageBroker>().Subscribe<NetworkCreatePartyVisual>(payload => created.Add(payload.What));
            client.Resolve<IMessageBroker>().Subscribe<NetworkDestroyPartyVisual>(payload => destroyed.Add(payload.What));

            var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
            uint partyHandle = 0;
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(server.ObjectManager.TryGetHandle(party, out partyHandle));
                Assert.DoesNotContain(server.ObjectManager.GetHandleMap().Values, handle =>
                    server.ObjectManager.TryGetObject<object>(handle, out var value) && value is MobilePartyVisual);
                party.RemoveParty();
            });

            var createdVisual = Assert.Single(created);
            Assert.Null(createdVisual.PartyVisualId);
            Assert.Equal(0u, createdVisual.PartyVisualHandle);
            Assert.Equal(partyHandle, createdVisual.MobilePartyHandle);
            Assert.Equal(partyHandle, Assert.Single(destroyed).MobilePartyHandle);
        }

        [Fact]
        public void RecreatingAHeadlessVisual_QueuesModelRefreshForTheExistingClientParty()
        {
            var server = TestEnvironment.Server;
            var client = TestEnvironment.Clients.First();
            var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));

            client.Call(() => clientParty.Party.OnVisualsUpdated());

            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var serverParty));
                var visuals = server.Resolve<IPartyVisualInterface>();
                visuals.RemoveVisual(serverParty);
                visuals.CreateVisual(serverParty);
            });

            client.Call(() => Assert.True(clientParty.Party.IsVisualDirty));
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
            uint expectedMobilePartyHandle = 0;
            client.Resolve<IMessageBroker>().Subscribe<NetworkDestroyPartyVisual>(payload => destroyed = payload.What);

            server.Call(() =>
            {
                var mobileParty = new MobileParty();
                var partyBase = new PartyBase(mobileParty);
                mobileParty.StringId = "mountain_bandits_24";
                Assert.True(server.ObjectManager.TryGetId(mobileParty, out string mobilePartyId));
                Assert.True(server.ObjectManager.TryGetHandle(mobileParty, out expectedMobilePartyHandle));
                Assert.True(server.ObjectManager.Remove(mobileParty));

                var mobilePartyVisual = new MobilePartyVisual(partyBase);

                Assert.False(server.ObjectManager.TryGetId(mobilePartyVisual, out _));
                Assert.True(server.ObjectManager.AddExisting(mobilePartyId, mobileParty));
                server.Resolve<IMessageBroker>().Publish(
                    this,
                    new PartyVisualDestroyed(mobilePartyVisual, mobileParty));
            });

            Assert.NotNull(destroyed);
            Assert.Equal(expectedMobilePartyHandle, destroyed.MobilePartyHandle);
        }
    }
}
