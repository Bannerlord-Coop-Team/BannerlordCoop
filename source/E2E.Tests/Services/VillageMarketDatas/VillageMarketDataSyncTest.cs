using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.VillageMarketDatas
{
    public class VillageMarketDataSyncTest : SyncTestBase
    {
        private string villageMarketDataId;

        public VillageMarketDataSyncTest(ITestOutputHelper output) : base(output)
        {
            villageMarketDataId = TestEnvironment.CreateRegisteredObject<VillageMarketData>();
            TestEnvironment.CreateRegisteredObject<Village>();
        }

        [Fact]
        public void Server_VillageMarketData_Fields()
        {
            TestEnvironment.Server.ObjectManager.TryGetObject(villageMarketDataId, out VillageMarketData villageMarketData);
            villageMarketData._village = null;
            TestEnvironment.AssertReferenceField<VillageMarketData, Village>(nameof(VillageMarketData._village));
        }
    }
}