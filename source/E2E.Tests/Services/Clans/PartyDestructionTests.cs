using E2E.Tests.Environment;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Clans;

public class ClanDestructionTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    Clan clan;
    public ClanDestructionTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        // Create a clan on the server and all clients
        TestEnvironment.Server.Call(() =>
        {
            clan = Clan.CreateClan("TestClan");
        });

        // Ensure clan was created on all clients
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Clan>(clan!.StringId, out var _));
        }
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerDestroyClan_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        server.Call(() =>
        {
            DestroyClanAction.Apply(clan);
        });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<Clan>(clan.StringId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Clan>(clan.StringId, out var _));
        }
    }

    [Fact]
    public void ClientDestroyClan_DoesNothing()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        TestEnvironment.Clients.First().Call(() =>
        {
            DestroyClanAction.Apply(clan);
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Clan>(clan.StringId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Clan>(clan.StringId, out var _));
        }
    }
}