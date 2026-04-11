using E2E.Tests.Util;
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
        //TestEnvironment.CreateRegisteredObject<Village.VillageStates>();
        TestEnvironment.CreateRegisteredObject<VillagerPartyComponent>();
        TestEnvironment.CreateRegisteredObject<PartyBase>();
        TestEnvironment.CreateRegisteredObject<Settlement>();
    }

    [Fact]
    public void Server_Village_Fields()
    {
        Server.ObjectManager.TryGetObject(villageId, out Village village);

        //TestEnvironment.AssertReferenceField<Village, VillageType>(nameof(Village.VillageType)); // VillageType needs to be managed or serialised
        TestEnvironment.AssertReferenceField<Village, VillageMarketData>(nameof(Village._marketData), null, null, village.MarketData);
        TestEnvironment.AssertField<Village, Village.VillageStates>(nameof(Village._villageState), Village.VillageStates.Looted);
        TestEnvironment.AssertReferenceField<Village, VillagerPartyComponent>(nameof(Village.VillagerPartyComponent));
        //TestEnvironment.AssertReferenceField<Village, PartyBase>(nameof(Village._owner)); // Uses abstract method PartyBase which can't be prepared. Not sure what to do about this
        TestEnvironment.AssertReferenceField<Village, Settlement>(nameof(Village._bound));
    }

    [Fact]
    public void Server_Village_Properties()
    {
        TestEnvironment.AssertProperty<Village, float>(nameof(Village.Hearth), 650f);
        TestEnvironment.AssertProperty<Village, float>(nameof(Village.LastDemandSatisfiedTime), 30, -1); // Taleworlds constructor defaults to -1
        TestEnvironment.AssertProperty<Village, int>(nameof(Village.TradeTaxAccumulated), 450);
    }
}
