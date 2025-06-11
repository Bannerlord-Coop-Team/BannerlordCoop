using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.VillageMarketDatas
{
    public class VillageMarketDataSyncTest : SyncTestBase
    {
        public VillageMarketDataSyncTest(ITestOutputHelper output) : base(output)
        {
            TestEnvironment.CreateRegisteredObject<VillageMarketData>();
            TestEnvironment.CreateRegisteredObject<Village>();
        }

        [Fact]
        public void Server_VillageMarketData_Fields()
        {
            TestEnvironment.AssertReferenceField<VillageMarketData, Village>(nameof(VillageMarketData._village));
        }
    }
}