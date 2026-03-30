using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Fiefs;
public class SyncFiefTests : SyncTestBase
{
    public SyncFiefTests(ITestOutputHelper output) : base(output)
    {
        TestEnvironment.CreateRegisteredObject<Town>();
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
        TestEnvironment.AssertReferenceField<Town, GarrisonPartyComponent>(nameof(Town.GarrisonPartyComponent));
    }
}