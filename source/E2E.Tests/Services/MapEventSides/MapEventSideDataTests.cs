using E2E.Tests.Environment;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEventSides;
public class MapEventSideDataTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    private readonly float newFloat = 9f;
    private readonly BattleSideEnum newSide = BattleSideEnum.Attacker;

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
            var mapEventSide = GameObjectCreator.CreateInitializedObject<MapEventSide>();
            var mobileParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

            Assert.True(server.ObjectManager.TryGetId(mapEventSide, out mapEventId));
            Assert.True(server.ObjectManager.TryGetId(mobileParty, out leaderPartyId));

            mapEventSide.LeaderParty = mobileParty.Party;
            mapEventSide.CasualtyStrength = newFloat;
            mapEventSide.MissionSide = newSide;
        });

        // Assert
        Assert.NotNull(mapEventId);

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEventSide>(mapEventId, out var mapEventSide));
            Assert.Equal(leaderPartyId, mapEventSide.LeaderParty.MobileParty.StringId);
            Assert.Equal(newFloat, mapEventSide.CasualtyStrength);
            Assert.Equal(newSide, mapEventSide.MissionSide);
        }
    }
}
