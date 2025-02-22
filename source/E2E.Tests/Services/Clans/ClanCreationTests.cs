using E2E.Tests.Environment;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Clans;

public class ClanCreationTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public ClanCreationTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerCreateClan_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        string? clanId = null;
        server.Call(() =>
        {
            var clan = Clan.CreateClan("TestClan");
            Assert.True(server.ObjectManager.TryGetId(clan, out clanId));
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Clan>(clanId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Clan>(clanId, out var _));
        }
    }

    [Fact]
    public void ClientCreateClan_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        var client = TestEnvironment.Clients.First();

        Clan? clan = null;
        client.Call(() =>
        {
            clan = Clan.CreateClan("TestClan");
        });

        // Assert
        Assert.NotNull(clan);
        Assert.False(server.ObjectManager.TryGetId(clan, out var _));
        Assert.False(client.ObjectManager.TryGetId(clan, out var _));
    }
}