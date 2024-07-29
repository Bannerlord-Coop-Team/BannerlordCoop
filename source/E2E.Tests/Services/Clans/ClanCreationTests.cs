using E2E.Tests.Environment;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
            var clan = Clan.CreateClan("");
            clanId = clan.StringId;
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
        string? clanId = null;
        TestEnvironment.Clients.First().Call(() =>
        {
            var clan = Clan.CreateClan("");
            clanId = clan.StringId;
        });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<Clan>(clanId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Clan>(clanId, out var _));
        }
    }
}