using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Fiefs;
public class SyncFiefTests : SyncTestBase
{
    private string TownId;

    public SyncFiefTests(ITestOutputHelper output) : base(output)
    {
        TownId = TestEnvironment.CreateRegisteredObject<Town>();
        TestEnvironment.CreateRegisteredObject<GarrisonPartyComponent>();
    }

    [Fact]
    public void Server_Fief_Properties()
    {
        TestEnvironment.AssertProperty<Town, float>(nameof(Town.FoodStocks), 5);
    }

    [Fact]
    public void Server_Fief_Fields()
    {
        // GarrisonPartyComponent may be initialized; clear it first so the pre-check passes
        Server.ObjectManager.TryGetObject<Town>(TownId, out var town);
        AccessTools.Field(typeof(Fief), nameof(Town.GarrisonPartyComponent)).SetValue(town, null);
        TestEnvironment.AssertReferenceField<Town, GarrisonPartyComponent>(nameof(Town.GarrisonPartyComponent));
    }
}