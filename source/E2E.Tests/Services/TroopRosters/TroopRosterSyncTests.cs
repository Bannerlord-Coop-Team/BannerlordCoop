using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.TroopRosters
{
    public class TroopRosterSyncTests : SyncTestBase
    {
        public TroopRosterSyncTests(ITestOutputHelper output) : base(output)
        {
            TestEnvironment.CreateRegisteredObject<TroopRoster>();
            TestEnvironment.CreateRegisteredObject<PartyBase>();
        }

        [Fact]
        public void Server_TroopRoster_Properties()
        {
            TestEnvironment.AssertReferenceProperty<TroopRoster, PartyBase>(nameof(TroopRoster.OwnerParty));
        }
    }
}
