using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Towns;
public class TownSyncTests : SyncTestBase
{
	public TownSyncTests(ITestOutputHelper output) : base(output)
	{
        TestEnvironment.CreateRegisteredObject<Town>();
        TestEnvironment.CreateRegisteredObject<Hero>();
	}


    [Fact]
    public void Server_Town_Fields()
    {

        // TODO: Fix patching for these 2 values
        //TestEnvironment.AssertField<Town, int>(nameof(Town._wallLevel), 1);
        //TestEnvironment.AssertField<Town, bool>(nameof(Town._isCastle), true);
        TestEnvironment.AssertField<Town, float>(nameof(Town._prosperity), 500f);
        TestEnvironment.AssertField<Town, int>(nameof(Town._tradeTax), 70);
        TestEnvironment.AssertField<Town, int>(nameof(Town.BoostBuildingProcess), 200);
        TestEnvironment.AssertField<Town, bool>(nameof(Town.InRebelliousState), true);
        TestEnvironment.AssertReferenceField<Town, Hero>(nameof(Town._governor));
    }

    [Fact]
    public void Server_Town_Properties()
    {
        TestEnvironment.AssertProperty<Town, float>(nameof(Town.Security), 50f);
        TestEnvironment.AssertProperty<Town, float>(nameof(Town.Loyalty), 60f);
        TestEnvironment.AssertReferenceProperty<Town, Hero>(nameof(Town.Governor));
    }
}
