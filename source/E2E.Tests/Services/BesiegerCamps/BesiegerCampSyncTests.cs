using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;
using TaleWorlds.CampaignSystem.Party;

namespace E2E.Tests.Services.BesiegerCamps
{
    public class BesiegerCampSyncTests : SyncTestBase
    {
        public BesiegerCampSyncTests(ITestOutputHelper output) : base(output)
        {
            TestEnvironment.CreateRegisteredObject<SiegeEvent>();
            TestEnvironment.CreateRegisteredObject<BesiegerCamp>();
            TestEnvironment.CreateRegisteredObject<MobileParty>();
        }

        [Fact]
        public void Server_BesiegerCamp_Fields()
        {
            TestEnvironment.AssertReferenceField<BesiegerCamp, MobileParty>(nameof(BesiegerCamp._leaderParty));
        }

        [Fact]
        public void Server_BesiegerCamp_Properties()
        {
            // Properties
            TestEnvironment.AssertProperty<BesiegerCamp, int>(nameof(BesiegerCamp.NumberOfTroopsKilledOnSide), 200);
            TestEnvironment.AssertReferenceProperty<BesiegerCamp, SiegeEvent>(nameof(BesiegerCamp.SiegeEvent));
            TestEnvironment.AssertReferenceProperty<BesiegerCamp, SiegeEnginesContainer>(nameof(BesiegerCamp.SiegeEngines));
            TestEnvironment.AssertReferenceProperty < BesiegerCamp, SiegeStrategy>(nameof(BesiegerCamp.SiegeStrategy));
        }
    }
}
