using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Hideouts
{
    public class HideoutSyncTests : SyncTestBase
    {
        private readonly string HideoutId;

        public HideoutSyncTests(ITestOutputHelper output) : base(output)
        {
            HideoutId = TestEnvironment.CreateRegisteredObject<Hideout>();
            TestEnvironment.CreateRegisteredObject<Settlement>();
        }

        [Fact]
        public void Server_Hideout_Fields()
        {
            TestEnvironment.AssertField<Hideout, CampaignTime>(nameof(Hideout._nextPossibleAttackTime), new CampaignTime(5133));
        }

        [Fact]
        public void Server_Hideout_Properties()
        {
            // v1.5 removed Hideout.IsSpotted: a spotted hideout is a visible settlement. A new settlement
            // starts visible and Hideout.OnInit hides it, so the sync is checked by hiding one.
            TestEnvironment.AssertProperty<Settlement, bool>(nameof(Settlement.IsVisible), false, defaultValue: true);
            //TestEnvironment.AssertProperty<Hideout, string>(nameof(Hideout.SceneName), "testScene");
        }
    }
}
