using Common;
using E2E.Tests.Environment;
using GameInterface.Registry.Auto;
using GameInterface.Services.PartyVisuals.Extensions;
using GameInterface.Services.PartyVisuals.Interfaces;
using GameInterface.Services.PartyVisuals.Messages;
using HarmonyLib;
using SandBox.View.Map.Managers;
using SandBox.View.Map.Visuals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyVisuals
{
    /// <summary>Exercises visual lifetime replication through the managed map visual collections.</summary>
    public class PartyVisualsLifetimeTests : IDisposable
    {
        private static MobilePartyVisualManager? clientVisualManager;
        private static MobilePartyVisualManager? serverVisualManager;

        E2ETestEnvironment TestEnvironment { get; }
        public PartyVisualsLifetimeTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output, numClients: 1);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PartyVisualLifecycle_AppliesInOrderByPartyHandle(bool graphicalServer)
        {
            var server = TestEnvironment.Server;
            var client = TestEnvironment.Clients.First();
            server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;
            var presentation = new Harmony($"e2e.party-visual-lifetime.{Guid.NewGuid():N}");

            uint partyHandle = 0;
            try
            {
                PatchVisualManagers(presentation, graphicalServer);

                // Create the party and apply its visual after its handle is registered.
                server.Call(() =>
                {
                    if (graphicalServer)
                    {
                        CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, party => party.CreateNewPartyVisual());
                    }

                    var party = MobileParty.CreateParty("visual_lifecycle_test", null);
                    Assert.True(server.ObjectManager.TryGetHandle(party, out partyHandle));
                });

                Assert.False(client.ObjectManager.TryGetObject<MobileParty>(partyHandle, out _));
                client.PumpGameThread();
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyHandle, out var clientParty));
                client.Call(() =>
                {
                    Assert.NotNull(clientVisualManager.GetVisualOfEntity(clientParty.Party));
                    clientParty.Party.OnVisualsUpdated();
                });
                Assert.Equal(partyHandle, Assert.Single(client.InternalMessages.GetMessages<NetworkCreatePartyVisual>()).MobilePartyHandle);

                // Remove only the visual and keep the party handle.
                server.Call(() =>
                {
                    Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyHandle, out var party));
                    server.Resolve<IPartyVisualInterface>().RemoveVisual(party);
                });

                client.PumpGameThread();
                client.Call(() => Assert.Null(clientVisualManager.GetVisualOfEntity(clientParty.Party)));
                Assert.Equal(partyHandle, Assert.Single(client.InternalMessages.GetMessages<NetworkDestroyPartyVisual>()).MobilePartyHandle);

                // Recreate the visual on that same handle and check the model refresh.
                server.Call(() =>
                {
                    Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyHandle, out var party));
                    server.Resolve<IPartyVisualInterface>().CreateVisual(party);
                });

                client.PumpGameThread();
                client.Call(() =>
                {
                    Assert.NotNull(clientVisualManager.GetVisualOfEntity(clientParty.Party));
                    Assert.True(clientParty.Party.IsVisualDirty);
                });
                Assert.Equal(new[] { partyHandle, partyHandle },
                    client.InternalMessages.GetMessages<NetworkCreatePartyVisual>().Select(message => message.MobilePartyHandle));
            }
            finally
            {
                server.Call(() => CampaignEvents.MobilePartyCreated.ClearListeners(this));
                presentation.UnpatchAll(presentation.Id);
                serverVisualManager = null;
                clientVisualManager = null;
            }
        }

        private static void PatchVisualManagers(Harmony presentation, bool graphicalServer)
        {
            clientVisualManager = new MobilePartyVisualManager();
            serverVisualManager = graphicalServer ? new MobilePartyVisualManager() : null;
            presentation.Patch(
                AccessTools.PropertyGetter(typeof(MobilePartyVisualManager), nameof(MobilePartyVisualManager.Current)),
                postfix: new HarmonyMethod(typeof(PartyVisualsLifetimeTests), nameof(ProvideVisualManager)));
            // Keep the native manager's collections and lifetime methods, skipping scene creation.
            presentation.Patch(
                AccessTools.Method(typeof(MobilePartyVisual), "OnStartup"),
                prefix: new HarmonyMethod(typeof(PartyVisualsLifetimeTests), nameof(SkipVisualStartup)));
        }

        private static void ProvideVisualManager(ref MobilePartyVisualManager? __result)
        {
            __result = ModInformation.IsServer ? serverVisualManager : clientVisualManager;
        }

        private static bool SkipVisualStartup() => false;

        [Fact]
        public void DestroyingAHeadlessParty_SendsVisualRemovalBeforePartyDestruction()
        {
            var server = TestEnvironment.Server;
            var client = TestEnvironment.Clients.First();
            var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
            var partyHandle = server.GetHandle<MobileParty>(partyId);

            server.Call(() => server.GetRegisteredObject<MobileParty>(partyId).RemoveParty());

            Assert.Collection(
                client.InternalMessages.Where(message =>
                    message is NetworkDestroyPartyVisual or NetworkDestroyInstance<MobileParty>),
                message => Assert.Equal(partyHandle, Assert.IsType<NetworkDestroyPartyVisual>(message).MobilePartyHandle),
                message => Assert.Equal(partyHandle, Assert.IsType<NetworkDestroyInstance<MobileParty>>(message).InstanceHandle));
        }

        [Fact]
        public void ReplicatedPartyActivity_RefreshesTheClientNameplateOncePerChange()
        {
            var server = TestEnvironment.Server;
            var client = TestEnvironment.Clients.First();
            var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var serverParty));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));
            var activityAtRefresh = new List<bool>();

            client.Call(() =>
            {
                Assert.True(clientParty.IsActive);
                CampaignEvents.PartyVisibilityChangedEvent.AddNonSerializedListener(this, party =>
                {
                    if (ReferenceEquals(party, clientParty.Party))
                        activityAtRefresh.Add(clientParty.IsActive);
                });
            });

            try
            {
                server.Call(() => serverParty.IsActive = false);
                server.Call(() => serverParty.IsActive = true);

                Assert.Equal(new[] { false, true }, activityAtRefresh);
            }
            finally
            {
                client.Call(() => CampaignEvents.PartyVisibilityChangedEvent.ClearListeners(this));
            }
        }
    }
}
