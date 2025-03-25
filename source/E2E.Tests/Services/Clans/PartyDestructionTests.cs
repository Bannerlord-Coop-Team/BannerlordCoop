using E2E.Tests.Environment;
using System.Security.Claims;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Clans;

public class ClanDestructionTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    string? clanId;
    public ClanDestructionTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        // Create a clan on the server and all clients
        TestEnvironment.Server.Call(() =>
        {
            var clan = Clan.CreateClan("TestClan");
            Assert.True(TestEnvironment.Server.ObjectManager.TryGetId(clan, out clanId));
        });

        Assert.NotNull(clanId);

        // Ensure clan was created on all clients
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Clan>(clanId, out var _));
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
            Assert.True(TestEnvironment.Server.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            DestroyClanAction.Apply(clan);
        });

        // Assert
        Assert.False(server.ObjectManager.TryGetObject<Clan>(clanId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<Clan>(clanId, out var _));
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
            Assert.True(TestEnvironment.Server.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            DestroyClanAction.Apply(clan);
        });

        // Assert
        Assert.True(server.ObjectManager.TryGetObject<Clan>(clanId, out var _));

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Clan>(clanId, out var _));
        }
    }
}