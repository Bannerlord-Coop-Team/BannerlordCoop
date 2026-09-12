using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Fiefs;
public class SyncFiefTests : SyncTestBase
{
    private readonly string TownId;
    private readonly string SettlementId;
    private readonly string GarrisonComponentId;

    public SyncFiefTests(ITestOutputHelper output) : base(output)
    {
        TownId = TestEnvironment.CreateRegisteredObject<Town>();
        SettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        GarrisonComponentId = TestEnvironment.CreateRegisteredObject<GarrisonPartyComponent>();
    }

    [Fact]
    public void Server_Fief_Properties()
    {
        TestEnvironment.AssertProperty<Town, float>(nameof(Town.FoodStocks), 5);
    }

    [Fact]
    public void Server_GarrisonLifecycle_SyncsFiefField()
    {
        ConfigureGarrisonGraph(Server);
        foreach (var client in TestEnvironment.Clients)
        {
            ConfigureGarrisonGraph(client);
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(TownId, out Town town));
            Assert.True(Server.ObjectManager.TryGetObject(GarrisonComponentId, out GarrisonPartyComponent component));

            component.OnInitialize();

            Assert.Same(component, town.GarrisonPartyComponent);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject(TownId, out Town town));
                Assert.True(client.ObjectManager.TryGetObject(GarrisonComponentId, out GarrisonPartyComponent component));
                Assert.Same(component, town.GarrisonPartyComponent);
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(TownId, out Town town));
            Assert.True(Server.ObjectManager.TryGetObject(GarrisonComponentId, out GarrisonPartyComponent component));

            component.OnFinalize();

            Assert.Null(town.GarrisonPartyComponent);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject(TownId, out Town town));
                Assert.Null(town.GarrisonPartyComponent);
            });
        }
    }

    private void ConfigureGarrisonGraph(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject(TownId, out Town town));
            Assert.True(instance.ObjectManager.TryGetObject(SettlementId, out Settlement settlement));
            Assert.True(instance.ObjectManager.TryGetObject(GarrisonComponentId, out GarrisonPartyComponent component));

            component.Settlement = settlement;
            settlement.Town = town;
            town.GarrisonPartyComponent = null;
        });
    }
}
