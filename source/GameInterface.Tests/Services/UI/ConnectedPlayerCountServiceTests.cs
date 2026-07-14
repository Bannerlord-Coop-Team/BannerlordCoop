using GameInterface.Services.UI;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Tests connected-player count state exposed to the encyclopedia UI.</summary>
public class ConnectedPlayerCountServiceTests
{
    [Fact]
    public void UpdateConnectedPlayers_ClampsAndNotifiesChangesOnly()
    {
        var service = new ConnectedPlayerCountService();
        int notifications = 0;
        service.ConnectedPlayersChanged += () => notifications++;

        service.UpdateConnectedPlayers(2);
        service.UpdateConnectedPlayers(2);
        service.UpdateConnectedPlayers(-1);

        Assert.Equal(0, service.ConnectedPlayers);
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void FormatEncyclopediaTitle_IncludesCurrentCount()
    {
        var service = new ConnectedPlayerCountService();
        service.UpdateConnectedPlayers(3);

        Assert.Equal("Encyclopedia (3 online)", service.FormatEncyclopediaTitle("Encyclopedia"));
    }
}
