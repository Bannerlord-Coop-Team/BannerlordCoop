using E2E.Tests.Util;
using Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
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
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party1));
            Assert.True(server.ObjectManager.TryGetId(party1.PartyComponent.MobileParty, out var clientParty2Id));

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

        // Force sync of MobileParty to clients: it's set during construction before clients have
        // the party in their ObjectManager, so the AutoSync message is dropped on clients.
        // Re-null the backing field and re-set via property to trigger a fresh sync.
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var p));
            AccessTools.Field(typeof(PartyComponent), "<MobileParty>k__BackingField").SetValue(p.PartyComponent, null);
            p.PartyComponent.MobileParty = p;
        });

        Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party1));
        Assert.True(server.ObjectManager.TryGetId(party1.PartyComponent.MobileParty, out var serverPartyId));

        // Act
        var firstClient = TestEnvironment.Clients.First();

        firstClient.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party1));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(party2Id, out var party2));

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
}