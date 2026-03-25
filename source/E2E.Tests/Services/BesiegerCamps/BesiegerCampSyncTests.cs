using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;
using TaleWorlds.CampaignSystem.Party;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;

namespace E2E.Tests.Services.BesiegerCamps
{
    public class BesiegerCampSyncTests : SyncTestBase
    {
        public BesiegerCampSyncTests(ITestOutputHelper output) : base(output)
        {
            TestEnvironment.CreateRegisteredObject<SiegeEvent>(new List<MethodBase>
                {
                    AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
                    AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
                    AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)),
                });
            TestEnvironment.CreateRegisteredObject<MobileParty>();
            TestEnvironment.CreateRegisteredObject<BesiegerCamp>(new List<MethodBase>
            {
                    AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
                    AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
                    AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)),
            });
            TestEnvironment.CreateRegisteredObject<SiegeEnginesContainer>();
            TestEnvironment.CreateRegisteredObject<SiegeStrategy>();
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
