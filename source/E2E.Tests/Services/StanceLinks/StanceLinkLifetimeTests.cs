using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.StanceLinks;

public class StanceLinkLifetimeTests : IDisposable
{
    private readonly List<MethodBase> disabledMethods;
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    public StanceLinkLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        disabledMethods = new List<MethodBase>
        {
            //Add your disabled methods
        };
    }
    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateStanceLink_SyncAllClients()
    {
        // Arrange
        string? stanceLinkId = null;

        // Act
        Server.Call(() =>
        {
            var stanceLink = GameObjectCreator.CreateInitializedObject<StanceLink>();
            Assert.True(Server.ObjectManager.TryGetId(stanceLink, out stanceLinkId));
        }, disabledMethods
        );

        // Assert
        Assert.NotNull(stanceLinkId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<StanceLink>(stanceLinkId, out var _));
        }
    }

    [Fact]
    public void ClientCreateStanceLink_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var firstClient = TestEnvironment.Clients.First();
        string? faction1Id = null;
        string? faction2Id = null;
        string? faction1StringId = null;
        string? faction2StringId = null;

        server.Call(() =>
        {
            var faction1 = Kingdom.CreateKingdom("test_kingdom_1");
            var faction2 = Kingdom.CreateKingdom("test_kingdom_2");
            faction1StringId = faction1.StringId;
            faction2StringId = faction2.StringId;

            Assert.True(server.ObjectManager.TryGetId(faction1, out faction1Id));
            Assert.True(server.ObjectManager.TryGetId(faction2, out faction2Id));
        });

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<IFaction>(faction1Id, out var _));
            Assert.True(client.ObjectManager.TryGetObject<IFaction>(faction2Id, out var _));
        }

        // Act
        string? clientStanceLinkId = null;
        StanceLink? clientStanceLink = null;
        StanceType? expectedStanceType = null;

        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<IFaction>(faction1Id, out var clientFaction1));
            Assert.True(firstClient.ObjectManager.TryGetObject<IFaction>(faction2Id, out var clientFaction2));

            clientStanceLink = FactionManager.Instance.GetStanceLinkInternal(clientFaction1, clientFaction2);
            expectedStanceType = clientStanceLink.StanceType;

            Assert.Equal(faction1StringId, clientStanceLink.Faction1.StringId);
            Assert.Equal(faction2StringId, clientStanceLink.Faction2.StringId);
        });

        firstClient.Call(() =>
            Assert.True(firstClient.ObjectManager.TryGetId(clientStanceLink!, out clientStanceLinkId)));

        // Assert
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<StanceLink>(clientStanceLinkId, out var serverStanceLink));
            Assert.Equal(faction1StringId, serverStanceLink.Faction1.StringId);
            Assert.Equal(faction2StringId, serverStanceLink.Faction2.StringId);
            Assert.Equal(expectedStanceType, serverStanceLink.StanceType);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<StanceLink>(clientStanceLinkId, out var stanceLink));
                Assert.Equal(faction1StringId, stanceLink.Faction1.StringId);
                Assert.Equal(faction2StringId, stanceLink.Faction2.StringId);
                Assert.Equal(expectedStanceType, stanceLink.StanceType);
            });
        }
    }
}
