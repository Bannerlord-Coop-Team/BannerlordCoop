using E2E.Tests.Util;
using Microsoft.Win32;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Villages;
public class VillageSyncTests : SyncTestBase
{
    string villageId;
    public VillageSyncTests(ITestOutputHelper output) : base(output)
	{
        villageId = TestEnvironment.CreateRegisteredObject<Village>();
        TestEnvironment.CreateRegisteredObject<Hero>();
        TestEnvironment.CreateRegisteredObject<Clan>();
        TestEnvironment.CreateRegisteredObject<VillageMarketData>();
        TestEnvironment.CreateRegisteredObject<VillagerPartyComponent>();
        //TestEnvironment.CreateRegisteredObject<VillageType>(); // Server object manager failed to register new object VillageType
        TestEnvironment.CreateRegisteredObject<PartyBase>();
        TestEnvironment.CreateRegisteredObject<Settlement>();
    }

    [Fact]
    public void Server_Village_Fields()
    {
        Server.ObjectManager.TryGetObject(villageId, out Village village);

        //TestEnvironment.AssertReferenceField<Village, VillageType>(nameof(Village.VillageType), defaultValue: village.VillageType); // Need VillageType in constructor
        TestEnvironment.AssertReferenceField<Village, VillageMarketData>(nameof(Village._marketData), null, null, village.MarketData);
        TestEnvironment.AssertField<Village, Village.VillageStates>(nameof(Village._villageState), Village.VillageStates.Looted);
        TestEnvironment.AssertReferenceField<Village, VillagerPartyComponent>(nameof(Village.VillagerPartyComponent));
        //TestEnvironment.AssertReferenceField<Village, PartyBase>(nameof(Village._owner)); // Failed to find intercept for _owner
        TestEnvironment.AssertReferenceField<Village, Settlement>(nameof(Village._bound));
        TestEnvironment.AssertReferenceField<Village, Settlement>(nameof(Village._tradeBound));
    }

    [Fact]
    public void Server_Village_Properties()
    {
        Server.ObjectManager.TryGetObject(villageId, out Village village);

        TestEnvironment.AssertProperty<Village, float>(nameof(Village.Hearth), 650f);
        TestEnvironment.AssertProperty<Village, float>(nameof(Village.LastDemandSatisfiedTime), 30, defaultValue: village.LastDemandSatisfiedTime);
        TestEnvironment.AssertProperty<Village, int>(nameof(Village.TradeTaxAccumulated), 450);
    }
}
