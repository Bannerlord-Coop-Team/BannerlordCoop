using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Settlements
{
    public class SettlementSyncTests : SyncTestBase
    {
        string settlementId;
        public SettlementSyncTests(ITestOutputHelper output) : base(output)
        {
            settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
            TestEnvironment.CreateRegisteredObject<Hero>();
            TestEnvironment.CreateRegisteredObject<CultureObject>();
            TestEnvironment.CreateRegisteredObject<Hideout>();
            TestEnvironment.CreateRegisteredObject<MilitiaPartyComponent>();
            TestEnvironment.CreateRegisteredObject<ItemRoster>();
            TestEnvironment.CreateRegisteredObject<MobileParty>();
            TestEnvironment.CreateRegisteredObject<Town>();
            TestEnvironment.CreateRegisteredObject<Village>();
        }

        [Fact]
        public void Server_Settlement_Fields()
        {
            Server.ObjectManager.TryGetObject(settlementId, out Settlement settlement);
            settlement._name = null;
            settlement._position = new CampaignVec2(new Vec2(0, 0), true); // Need to assign a default position. Regular default of CampaignVec2.Invalid will not work as NaN != NaNs

            //TestEnvironment.AssertField<Settlement, int>(nameof(Settlement.CanBeClaimed), 3);
            //TestEnvironment.AssertReferenceField<Settlement, Hero>(nameof(Settlement.ClaimedBy));
            //TestEnvironment.AssertField<Settlement, float>(nameof(Settlement.ClaimValue), 2f);
            TestEnvironment.AssertReferenceField<Settlement, CultureObject>(nameof(Settlement.Culture));
            TestEnvironment.AssertField<Settlement, bool>(nameof(Settlement.HasVisited), true);
            TestEnvironment.AssertReferenceField<Settlement, Hideout>(nameof(Settlement.Hideout));
            TestEnvironment.AssertField<Settlement, float>(nameof(Settlement.LastVisitTimeOfOwner), 20f, defaultValue: settlement.LastVisitTimeOfOwner);
            TestEnvironment.AssertReferenceField<Settlement, MilitiaPartyComponent>(nameof(Settlement.MilitiaPartyComponent));

            // readonly
            //TestEnvironment.AssertReferenceField<Settlement, ItemRoster>(nameof(Settlement.Stash));
            TestEnvironment.AssertReferenceField<Settlement, Town>(nameof(Settlement.Town));
            TestEnvironment.AssertReferenceField<Settlement, Village>(nameof(Settlement.Village));
            TestEnvironment.AssertField<Settlement, bool>(nameof(Settlement._isVisible), false, defaultValue: true);
            TestEnvironment.AssertReferenceField<Settlement, MobileParty>(nameof(Settlement._lastAttackerParty));
            TestEnvironment.AssertField<Settlement, TextObject>(nameof(Settlement._name), new TextObject("test text")); //TEXTOBJECT
            TestEnvironment.AssertReferenceField<Settlement, Settlement>(nameof(Settlement._nextLocatable));
            TestEnvironment.AssertField<Settlement, int>(nameof(Settlement._numberOfLordPartiesAt), 7);
            // NumberOfLordPartiesTargeting is not synced - it's server-only AI data recomputed each tick
            //TestEnvironment.AssertField<Settlement, int>(nameof(Settlement.NumberOfLordPartiesTargeting), 2);
            TestEnvironment.AssertField<Settlement, CampaignVec2>(nameof(Settlement._position), new CampaignVec2(new Vec2(1,2), false), settlementId, new CampaignVec2(new Vec2(0, 0), true));
            TestEnvironment.AssertField<Settlement, float>(nameof(Settlement._readyMilitia), 5f);
            //TestEnvironment.AssertField<Settlement, Vec2>(nameof(Settlement._gatePosition), new Vec2(0, 1));
        }

        [Fact]
        public void Server_Settlement_Properties()
        {
            Server.ObjectManager.TryGetObject(settlementId, out Settlement settlement);


        }
    }
}