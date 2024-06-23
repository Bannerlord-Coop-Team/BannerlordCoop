using Coop.IntegrationTests.Environment.Instance;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Armies.Extensions;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;
public class MobilePartyPropertyTests : IDisposable
{
    E2ETestEnvironment TestEnvironement { get; }

    EnvironmentInstance Server => TestEnvironement.Server;
    IEnumerable<EnvironmentInstance> Clients => TestEnvironement.Clients;

    string PartyId { get; set; }

    public MobilePartyPropertyTests(ITestOutputHelper output)
    {
        TestEnvironement = new E2ETestEnvironment(output);


        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            GameObjectCreator.CreateInitializedObject<Clan>();

            PartyId = party.StringId;
            party.CustomName = new TextObject("DefaultName");
        });
    }

    public void Dispose()
    {
        TestEnvironement.Dispose();
    }

    [Fact]
    public void ServerChangeCustomName_SyncAllClients()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        Server.Call(() =>
        {
            serverParty.CustomName = new TextObject("NewTestCustomName");
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.CustomName.Value, serverParty.CustomName.Value);
        }
    }

    [Fact]
    public void ClientChangeCustomName_NoChange()
    {
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(PartyId, out var serverParty));

        // Act
        var firstClient = Clients.First();


        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            clientParty.CustomName = new TextObject("NewTestCustomName");
        });


        // Assert
        foreach (var client in TestEnvironement.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(PartyId, out var clientParty));
            Assert.Equal(clientParty.CustomName.Value, serverParty.CustomName.Value);
        }
    }
}