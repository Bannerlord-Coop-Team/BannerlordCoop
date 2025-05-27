using E2E.Tests.Util;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Towns;
public class TownSyncTests : SyncTestBase
{
    private string townId;

	public TownSyncTests(ITestOutputHelper output) : base(output)
	{
        townId = TestEnvironment.CreateRegisteredObject<Town>();
        TestEnvironment.CreateRegisteredObject<Hero>();
	}


    [Fact]
    public void Server_Town_Fields()
    {
        TestEnvironment.AssertField<Town, int>(nameof(Town._wallLevel), 1);
        TestEnvironment.AssertField<Town, bool>(nameof(Town._isCastle), true);
        TestEnvironment.AssertField<Town, float>(nameof(Town._prosperity), 500f);
        TestEnvironment.AssertField<Town, int>(nameof(Town._tradeTax), 70);
        TestEnvironment.AssertField<Town, int>(nameof(Town.BoostBuildingProcess), 200);
        TestEnvironment.AssertField<Town, bool>(nameof(Town.InRebelliousState), true);

        // Should work but currently the client side list of buildings is null instead of empty
        var buildingId = TestEnvironment.CreateRegisteredObject<Building>();
        var buildingsInfo = AccessTools.Field(typeof(Town), nameof(Town.Buildings));
        var buildingIntercept = TestEnvironment.GetCollectionAddIntercept(buildingsInfo);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Town>(townId, out var serverInstance));
            Assert.True(Server.ObjectManager.TryGetObject<Building>(buildingId, out var buildingInstance));
            buildingIntercept.Invoke(null, new object[] { serverInstance.Buildings, buildingInstance, serverInstance });
        });

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Town>(townId, out var clientInstance));
            Assert.True(client.ObjectManager.TryGetId<Building>(clientInstance.Buildings.Last(), out string id));

            Assert.Equal(buildingId, id);

        }
    }

    [Fact]
    public void Server_Town_Properties()
    {
        TestEnvironment.AssertProperty<Town, float>(nameof(Town.Security), 50f);
        TestEnvironment.AssertProperty<Town, float>(nameof(Town.Loyalty), 60f);
        TestEnvironment.AssertReferenceProperty<Town, Hero>(nameof(Town.Governor));
    }
}
