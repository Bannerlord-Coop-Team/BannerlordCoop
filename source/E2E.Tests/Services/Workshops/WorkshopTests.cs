using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Workshops
{
    public class WorkshopTests : SyncTestBase
    {
        public WorkshopTests(ITestOutputHelper output) : base(output)
        {
            TestEnvironment.CreateRegisteredObject<Workshop>();
            TestEnvironment.CreateRegisteredObject<WorkshopType>();
            TestEnvironment.CreateRegisteredObject<Hero>();
            TestEnvironment.CreateRegisteredObject<Settlement>();
        }

        [Fact]
        public void Server_Workshop_Properties()
        {
            TestEnvironment.AssertProperty<Workshop, int>(nameof(Workshop.Capital), 5);
            TestEnvironment.AssertProperty<Workshop, int>(nameof(Workshop.InitialCapital), 5);
            TestEnvironment.AssertProperty<Workshop, CampaignTime>(nameof(Workshop.LastRunCampaignTime), new CampaignTime(500));

            //CustomName? TextObject
            //TestEnvironment.AssertReferenceProperty<Workshop, WorkshopType>(nameof(Workshop.WorkshopType));
        }

        [Fact]
        public void Server_Workshop_Fields()
        {
            TestEnvironment.AssertField<Workshop, string>(nameof(Workshop._tag), "tag");

            TestEnvironment.AssertReferenceField<Workshop, Hero>(nameof(Workshop._owner));
            TestEnvironment.AssertReferenceField<Workshop, Settlement>(nameof(Workshop._settlement));
        }        
    }
}