using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Towns;
public class SyncTownTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    EnvironmentInstance Server => TestEnvironment.Server;

    IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    private readonly string TownId;

    public SyncTownTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        var town = new Town();

        // Create fief on the server
        Assert.True(Server.ObjectManager.AddNewObject(town, out TownId));

        // Create fief on all clients
        foreach (var client in Clients)
        {
            var client_town = new Town();
            Assert.True(client.ObjectManager.AddExisting(TownId, client_town));
        }
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void Server_Town_OwnerClan()
    {
        // Arrange
        var server = TestEnvironment.Server;

        var _hero = ObjectHelper.SkipConstructor<Hero>();
        server.ObjectManager.AddNewObject(_hero, out var heroId);

        foreach (var client in Clients)
        {
            var client_hero = ObjectHelper.SkipConstructor<Hero>();
            client.ObjectManager.AddExisting(heroId, client_hero);
        }


        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Town>(TownId, out var fief));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            fief.Governor = hero;
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Town>(TownId, out var fief));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var client_hero));

            Assert.Same(client_hero, fief.Governor);
        }
    }
}
