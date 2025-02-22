using TaleWorlds.CampaignSystem;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using Xunit.Abstractions;
using Common.Util;
using static Common.Extensions.ReflectionExtensions;

namespace E2E.Tests.Services.StanceLinks;

public class StanceLinkPropertyTests : IDisposable
{
    private readonly List<MethodBase> disabledMethods;
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

    private readonly string stanceLinkId;
    private readonly string faction1Id;
    private readonly string faction2Id;

    public StanceLinkPropertyTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        disabledMethods = new List<MethodBase>
        {
            //Add your disabled methods
        };

        // Create StanceLink on the server
        faction1Id = TestEnvironment.CreateRegisteredObject<Kingdom>(disabledMethods);
        faction2Id = TestEnvironment.CreateRegisteredObject<Kingdom>(disabledMethods);
        stanceLinkId = TestEnvironment.CreateRegisteredObject<StanceLink>(disabledMethods);

    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }


    [Fact]
    public void ServerChangeStanceLinkStanceType_SyncAllClients()
    {
        // Arrange
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        var newValue = Random<StanceType>();

        // Act
        Server.Call(() =>
        {
            serverStanceLink.StanceType = newValue;
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            Assert.Equal(serverStanceLink.StanceType, clientStanceLink.StanceType);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkFaction1_SyncAllClients()
    {
        // Arrange
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(faction1Id, out var serverFaction1));

        // Act
        Server.Call(() =>
        {
            serverStanceLink.Faction1 = serverFaction1;
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            client.ObjectManager.TryGetId(clientStanceLink.Faction1, out string clientFaction1Id);
            Assert.Equal(clientFaction1Id, faction1Id);
        }
    }

    [Fact]
    public void ServerChangeStanceLinkFaction2_SyncAllClients()
    {
        // Arrange
        string? faction2Id = null;
        Assert.True(Server.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var serverStanceLink));
        Assert.True(Server.ObjectManager.TryGetObject<IFaction>(faction2Id, out var serverFaction2));

        // Act
        Server.Call(() =>
        {
            serverStanceLink.Faction2 = serverFaction2;
        });

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var clientStanceLink));
            client.ObjectManager.TryGetId(clientStanceLink.Faction2, out string clientFaction2Id);
            Assert.Equal(clientFaction2Id, faction2Id);
        }
    }
}
