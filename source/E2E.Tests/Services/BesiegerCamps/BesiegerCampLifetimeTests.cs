using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BesiegerCamps;
public class BesiegerCampLifetimeTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    public BesiegerCampLifetimeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreate_BesiegerCamp_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? beseigerCampId = null;
        server.Call(() =>
        {
            var beseigerCamp = GameObjectCreator.CreateInitializedObject<BesiegerCamp>();

            Assert.True(server.ObjectManager.TryGetId(beseigerCamp, out beseigerCampId));
        });

        // Assert
        Assert.NotNull(beseigerCampId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(beseigerCampId, out var _));
        }
    }

    [Fact]
    public void ClientCreate_BesiegerCamp_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;

        string? mapEventId = null;
        string? mobilePartyId = null;
        server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

            Assert.True(server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.True(server.ObjectManager.TryGetId(mobileParty, out mobilePartyId));
        });

        Assert.NotNull(mapEventId);
        Assert.NotNull(mobilePartyId);

        // Act
        string? clientBeseigerCampId = null;

        var firstClient = TestEnvironment.Clients.First();
        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));

            var BesiegerCamp = new BesiegerCamp(null);

            Assert.False(firstClient.ObjectManager.TryGetId(BesiegerCamp, out clientBeseigerCampId));
        });

        // Assert
        Assert.Null(clientBeseigerCampId);
    }
}
