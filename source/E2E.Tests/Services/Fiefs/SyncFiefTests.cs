using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Fiefs;
public class SyncFiefTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    EnvironmentInstance Server => TestEnvironment.Server;

    IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

    private readonly string FiefId;

    public SyncFiefTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        var fief = new Town();

        // Create fief on the server
        Assert.True(Server.ObjectManager.AddNewObject(fief, out FiefId));

        // Create fief on all clients
        foreach (var client in Clients)
        {
            var client_fief = new Town();
            Assert.True(client.ObjectManager.AddExisting(FiefId, client_fief));
        }
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }


    [Fact]
    public void Server_Fief_GarrisonPartyComponent()
    {
        // Arrange
        var server = TestEnvironment.Server;

        var field = AccessTools.Field(typeof(Fief), nameof(Fief.GarrisonPartyComponent));

        // Get field intercept to use on the server to simulate the field changing
        var intercept = TestEnvironment.GetIntercept(field);

        // Create garrison instances on server
        GarrisonPartyComponent garrison = ObjectHelper.SkipConstructor<GarrisonPartyComponent>();
        Assert.True(server.ObjectManager.AddNewObject(garrison, out var garrisonId));

        // Create garrison instances on all clients
        foreach (var client in Clients)
        {
            var client_garrison = ObjectHelper.SkipConstructor<GarrisonPartyComponent>();
            Assert.True(client.ObjectManager.AddExisting(garrisonId, client_garrison));
        }

        // Act
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Town>(FiefId, out var fief));
            Assert.True(server.ObjectManager.TryGetObject<GarrisonPartyComponent>(garrisonId, out var garrisonComponent));

            Assert.Null(fief.GarrisonPartyComponent);

            // Simulate the field changing
            intercept.Invoke(null, new object[] { fief, garrisonComponent });

            Assert.Same(garrisonComponent, fief.GarrisonPartyComponent);
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Town>(FiefId, out var fief));

            Assert.True(client.ObjectManager.TryGetObject<GarrisonPartyComponent>(garrisonId, out var clientComponent));

            Assert.True(clientComponent == fief.GarrisonPartyComponent);
        }
    }


    [Fact]
    public void Server_Fief_FoodStacks()
    {
        // Arrange
        var server = TestEnvironment.Server;

        // Act
        const float newValue = 551;
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Town>(FiefId, out var fief));

            fief.FoodStocks = newValue;

            Assert.Equal(newValue, fief.FoodStocks);
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Town>(FiefId, out var fief));

            Assert.Equal(newValue, fief.FoodStocks);
        }
    }
}
