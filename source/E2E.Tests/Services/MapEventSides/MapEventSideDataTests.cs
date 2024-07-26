using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEventSides;
public class MapEventSideDataTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    public MapEventSideDataTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void Server_MapEventSide_ChangeLeaderParty_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? mapEventId = null;
        string? leaderPartyId = null;
        server.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEventSide>();
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

            Assert.True(server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.True(server.ObjectManager.TryGetId(mobileParty, out leaderPartyId));

            mapEvent.LeaderParty = mobileParty.Party;
        });

        // Assert
        Assert.NotNull(mapEventId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEventSide>(mapEventId, out var mapEventSide));
            Assert.Equal(leaderPartyId, mapEventSide.LeaderParty.MobileParty.StringId);
        }
    }
}
