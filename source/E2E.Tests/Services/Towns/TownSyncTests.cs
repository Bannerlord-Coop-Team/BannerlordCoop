using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Towns;
public class TownSyncTests : SyncTestBase
{
    string townId;
    public TownSyncTests(ITestOutputHelper output) : base(output)
	{
        townId = TestEnvironment.CreateRegisteredObject<Town>();
        TestEnvironment.CreateRegisteredObject<Hero>();
        TestEnvironment.CreateRegisteredObject<Clan>();
    }

    [Fact]
    public void Server_Town_Fields()
    {
        Server.ObjectManager.TryGetObject(townId, out Town town);

        TestEnvironment.AssertField<Town, float>(nameof(Town._prosperity), 500f);
        TestEnvironment.AssertField<Town, bool>(nameof(Town._isCastle), true, defaultValue: town._isCastle);
        TestEnvironment.AssertField<Town, bool>(nameof(Town.GarrisonAutoRecruitmentIsEnabled), false, defaultValue: town.GarrisonAutoRecruitmentIsEnabled);
        TestEnvironment.AssertReferenceField<Town, Clan>(nameof(Town._ownerClan));
        //TestEnvironment.AssertCollectionReferenceField<Town, Village>(nameof(Town._tradeBoundVillagesCache));
        //TestEnvironment.AssertCollectionReferenceField<Town, Building>(nameof(Town.Buildings), townId);
        //TestEnvironment.AssertQueueReferenceField<Town, Building>(nameof(Town.BuildingsInProgress));
        TestEnvironment.AssertField<Town, int>(nameof(Town.BoostBuildingProcess), 200);
        TestEnvironment.AssertField<Town, bool>(nameof(Town.InRebelliousState), true, defaultValue: town.InRebelliousState);

        //TestEnvironment.AssertReferenceField<Town, int>(nameof(Town._marketData)); // readonly
    }

    [Fact]
    public void Server_Town_Properties()
    {
        TestEnvironment.AssertReferenceProperty<Town, Hero>(nameof(Town.Governor));
        Server.NetworkSentMessages.Clear();
        TestEnvironment.AssertProperty<Town, int>(nameof(Town.TradeTaxAccumulated), 200);
        AssertSingleAutoSyncMessageForPair("Town_TradeTaxAccumulated_SetNetworkMessage", "Town__tradeTax_SetNetworkMessage");
        Server.NetworkSentMessages.Clear();
        TestEnvironment.AssertProperty<Town, float>(nameof(Town.Security), 50f);
        AssertSingleAutoSyncMessageForPair("Town_Security_SetNetworkMessage", "Town__security_SetNetworkMessage");
        Server.NetworkSentMessages.Clear();
        TestEnvironment.AssertProperty<Town, float>(nameof(Town.Loyalty), 60f);
        AssertSingleAutoSyncMessageForPair("Town_Loyalty_SetNetworkMessage", "Town__loyalty_SetNetworkMessage");
        TestEnvironment.AssertReferenceProperty<Town, Clan>(nameof(Town.LastCapturedBy));
        TestEnvironment.AssertArrayReferenceProperty<Town, Workshop>(nameof(Town.Workshops));
    }
}
