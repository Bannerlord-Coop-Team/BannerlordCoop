using E2E.Tests.Util;
using Common.Logging;
using Common.Util;
using Common.Serialization;
using GameInterface.Services.PartyComponents.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.PartyComponents;
public class PartyComponentTests : SyncTestBase
{
    public PartyComponentTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void UnboundWarPartyBanner_ReportsCallerAndPreservesNullFallback()
    {
        var logs = new List<string>();
        Action<string> capture = message => { lock (logs) logs.Add(message); };
        OutputSinkManager.AddLogCallback(capture);
        try
        {
            TestEnvironment.Clients.First().Call(() =>
            {
                var component = (LordPartyComponent)FormatterServices.GetUninitializedObject(typeof(LordPartyComponent));
                Assert.Null(component.GetDefaultComponentBanner());
                Assert.Null(component.MobileParty);
            });
            lock (logs)
            {
                Assert.Contains(logs, log => log.Contains("MobileParty is null") &&
                    log.Contains(nameof(LordPartyComponent)) && log.Contains("caller=") &&
                    log.Contains(nameof(UnboundWarPartyBanner_ReportsCallerAndPreservesNullFallback)));
            }
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(capture);
        }
    }

    [Fact]
    public void ServerReplacement_EveryReceivedBindingKeepsBothDirectionsAndFlagsTogether()
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var componentId = TestEnvironment.CreateRegisteredObject<LordPartyComponent>();
        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<LordPartyComponent>(componentId, out var component));
            party.SetPartyComponent(component, false);
        });
        var messages = Server.NetworkSentMessages.Where(message =>
            message is NetworkPartyComponentMobilePartyUpdated ||
            message.GetType().Name.Contains("_partyComponent_SetNetworkMessage")).ToArray();
        Assert.NotEmpty(messages);

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<LordPartyComponent>(componentId, out var component));
            client.Call(() =>
            {
                using var setup = new AllowedThread();
                party._partyComponent = null;
                party.UpdatePartyComponentFlags();
                component.MobileParty = null;
            });
            client.NetworkSentMessages.Clear();
            foreach (var message in messages)
            {
                var bytes = Server.Resolve<ICommonSerializer>().Serialize(message);
                client.SimulateNetworkPayload(Server.NetPeer, bytes, markGameThread: false);
                Assert.True(client.PendingGameThreadActionCount > 0);
                client.PumpGameThread();
                client.Call(() =>
                {
                    Assert.Same(component, party.PartyComponent);
                    Assert.Same(party, component.MobileParty);
                    Assert.True(party.IsLordParty);
                    _ = party.Banner;
                });
            }
            Assert.Empty(client.NetworkSentMessages);
        }
    }

    [Fact]
    public void ServerCreateParty_BindsComponentOnEveryClientWithoutResendingSetter()
    {
        var componentId = TestEnvironment.CreateRegisteredObject<LordPartyComponent>();
        string partyId = null!;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<LordPartyComponent>(componentId, out var component));
            var party = MobileParty.CreateParty("binding-creation", component);
            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
        });
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(client.ObjectManager.TryGetObject<LordPartyComponent>(componentId, out var component));
                Assert.Same(component, party.PartyComponent);
                Assert.Same(party, component.MobileParty);
                Assert.True(party.IsLordParty);
            });
        }
    }

    [Fact]
    public void ServerClearComponent_ClearsReferenceAndTypeFlagsOnEveryClient()
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            party.SetPartyComponent(null);
        });
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.Null(party.PartyComponent);
                Assert.False(party.IsLordParty);
            });
        }
    }

    [Fact]
    public void ServerChange_MobileParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var party2Id = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party1));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(party2Id, out var party2));

            party1.PartyComponent.MobileParty = party2;
        });

        // Assert

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party1));
            Assert.True(client.ObjectManager.TryGetId(party1.PartyComponent.MobileParty, out var clientParty2Id));

            Assert.Equal(clientParty2Id, party2Id);
        }
    }

    [Fact]
    public void ClientChange_MobileParty_NoChange()
    {
        // Arrange
        var server = TestEnvironment.Server;

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var party2Id = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party1));
        Assert.True(server.ObjectManager.TryGetId(party1.PartyComponent.MobileParty, out var serverPartyId));

        // Act
        var firstClient = TestEnvironment.Clients.First();

        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(partyId, out var party1));
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(party2Id, out var party2));

            party1.PartyComponent.MobileParty = party2;
        });

        // Assert
        foreach (var client in TestEnvironment.Clients.Where(client => client != firstClient))
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));
            Assert.True(client.ObjectManager.TryGetId(clientParty.PartyComponent.MobileParty, out var clientPartyId));
            Assert.Equal(serverPartyId, clientPartyId);
        }
    }

    [Fact]
    public void ClientCreatePartyComponents_DoNotRegister()
    {
        // Arrange
        var client1 = TestEnvironment.Clients.First();

        Hero caravanHero = null;
        Hero lordHero = null;
        Settlement caravanSettlement = null;
        PartyTemplateObject caravanTemplate = null;
        Server.Call(() =>
        {
            caravanHero = GameObjectCreator.CreateInitializedObject<Hero>();
            lordHero = GameObjectCreator.CreateInitializedObject<Hero>();
            caravanSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            caravanTemplate = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
        });

        var garrisonSettlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        garrisonSettlement.Town = GameObjectCreator.CreateInitializedObject<Town>();
        var village = GameObjectCreator.CreateInitializedObject<Village>();
        var customSettlement = new Settlement();
        var customClan = new Clan();
        var lordSettlement = new Settlement();

        // Act
        var components = new List<PartyComponent>();
        client1.Call(() =>
        {
            var hideout = GameObjectCreator.CreateInitializedObject<Hideout>();
            var banditClan = GameObjectCreator.CreateInitializedObject<Clan>();
            var banditTemplate = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
            var position = new CampaignVec2(new Vec2(2, 2), true);

            components.Add(new BanditPartyComponent(hideout, false, new BanditPartyComponent.InitializationArgs(banditClan, banditTemplate, position)));
            components.Add(new CaravanPartyComponent(caravanSettlement, caravanHero, caravanHero, false, new CaravanPartyComponent.InitializationArgs(caravanTemplate)));
            components.Add(new CustomPartyComponent(customSettlement, new TextObject(""), new Hero(), "test", "testH", 1f, false,
                new CustomPartyComponent.InitializationArgs(position, 2f, customClan)));
            components.Add(new GarrisonPartyComponent(garrisonSettlement, new GarrisonPartyComponent.InitializationArgs()));
            components.Add(new LordPartyComponent(lordHero, lordHero, new LordPartyComponent.InitializationArgs(position, 2f, lordSettlement)));
            components.Add(new VillagerPartyComponent(village, new VillagerPartyComponent.InitializationArgs()));
        });

        // Assert
        Assert.Equal(6, components.Count);
        Assert.All(components, component => Assert.False(client1.ObjectManager.TryGetId(component, out _)));
    }
}
