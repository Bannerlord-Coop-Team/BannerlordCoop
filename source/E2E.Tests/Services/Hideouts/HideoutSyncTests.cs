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
        }

        [Fact]
        public void Server_Hideout_Fields()
        {
            TestEnvironment.AssertField<Hideout, bool>(nameof(Hideout._isSpotted), true);
            TestEnvironment.AssertField<Hideout, CampaignTime>(nameof(Hideout._nextPossibleAttackTime), new CampaignTime(5133));
        }

        [Fact]
        public void Server_Hideout_Properties()
        {
            //TestEnvironment.AssertProperty<Hideout, string>(nameof(Hideout.SceneName), "testScene");
        }
    }
}
