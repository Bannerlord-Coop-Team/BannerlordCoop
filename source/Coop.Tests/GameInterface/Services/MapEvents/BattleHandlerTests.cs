using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.Players.Data;
using Xunit;

namespace Coop.Tests.GameInterface.Services.MapEvents;

public class BattleHandlerTests
{
    [Fact]
    public void MapEventPlayerCount_ExcludesOfflineRegistrations()
    {
        var offline = new Player("offline", null, "offline-party", null, null);
        var connected = new Player("connected", null, "connected-party", null, null);
        int mapEventLookups = 0;

        int count = BattleHandler.CountConnectedPlayersInMapEvents(
            new[] { offline, connected },
            player => ReferenceEquals(player, connected),
            _ =>
            {
                mapEventLookups++;
                return true;
            });

        Assert.Equal(1, count);
        Assert.Equal(1, mapEventLookups);
    }
}
