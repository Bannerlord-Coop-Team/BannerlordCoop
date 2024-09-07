using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Fiefs;
public class FiefFieldTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    EnvironmentInstance Server => TestEnvironment.Server;

    IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;


    private int Ctr { get
        {
            return ctr++;
        }
    }
    private int ctr = 0;

    private readonly string FiefId;
    private readonly string MapEventSideId;
    private readonly ITestOutputHelper output;

    public FiefFieldTests(ITestOutputHelper output)
    {
        this.output = output;

        TestEnvironment = new E2ETestEnvironment(output);

        var fief = new Town();

        fief.StringId = "TestTown";
        

        Assert.True(Server.ObjectManager.AddNewObject(fief, out FiefId));
        

        foreach (var client in Clients)
        {
            var client_fief = new Town();
            client_fief.StringId = FiefId;

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

        var intercept = TestEnvironment.GetIntercept(field);

        GarrisonPartyComponent garrison = ObjectHelper.SkipConstructor<GarrisonPartyComponent>();
        Assert.True(server.ObjectManager.AddNewObject(garrison, out var garrisonId));

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

            intercept.Invoke(null, new object[] { fief, garrisonComponent });

            Assert.Same(garrisonComponent, fief.GarrisonPartyComponent);
        });

        // Assert
        var t = server.NetworkSentMessages.ToArray();

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Town>(FiefId, out var fief));

            Assert.True(client.ObjectManager.TryGetObject<GarrisonPartyComponent>(garrisonId, out var clientComponent));

            Assert.True(clientComponent == fief.GarrisonPartyComponent);
        }
    }
}
