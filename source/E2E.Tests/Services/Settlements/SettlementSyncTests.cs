using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
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
            TestEnvironment.CreateRegisteredObject<PartyBase>();
            //TestEnvironment.CreateRegisteredObject<SiegeEvent>(); // Object reference not set to an instance of an object.
            TestEnvironment.CreateRegisteredObject<Town>();
            TestEnvironment.CreateRegisteredObject<Village>();
        }

        [Fact]
        public void Server_Settlement_Fields()
        {
            Server.ObjectManager.TryGetObject(settlementId, out Settlement settlement);
            settlement._name = null;
            settlement._position = new CampaignVec2(new Vec2(0, 0), true); // Need to assign a default position. Regular default of CampaignVec2.Invalid will not work as NaN != NaN

            TestEnvironment.AssertReferenceField<Settlement, CultureObject>(nameof(Settlement.Culture));
            TestEnvironment.AssertField<Settlement, bool>(nameof(Settlement.HasVisited), true);
            TestEnvironment.AssertReferenceField<Settlement, Hideout>(nameof(Settlement.Hideout));
            TestEnvironment.AssertField<Settlement, float>(nameof(Settlement.LastVisitTimeOfOwner), 20f, defaultValue: settlement.LastVisitTimeOfOwner);
            TestEnvironment.AssertReferenceField<Settlement, MilitiaPartyComponent>(nameof(Settlement.MilitiaPartyComponent));
            
            TestEnvironment.AssertReferenceField<Settlement, Town>(nameof(Settlement.Town));
            TestEnvironment.AssertReferenceField<Settlement, Village>(nameof(Settlement.Village));
            TestEnvironment.AssertField<Settlement, bool>(nameof(Settlement._isVisible), false, defaultValue: settlement._isVisible);
            TestEnvironment.AssertReferenceField<Settlement, MobileParty>(nameof(Settlement._lastAttackerParty));
            TestEnvironment.AssertField<Settlement, TextObject>(nameof(Settlement._name), new TextObject("test text"));
            TestEnvironment.AssertReferenceField<Settlement, Settlement>(nameof(Settlement._nextLocatable));
            TestEnvironment.AssertField<Settlement, CampaignVec2>(nameof(Settlement._position), new CampaignVec2(new Vec2(1,2), false), defaultValue: settlement._position);
            TestEnvironment.AssertField<Settlement, float>(nameof(Settlement._readyMilitia), 5f);
            //TestEnvironment.AssertCollectionReferenceField<Settlement, Village>(nameof(Settlement._boundVillages));
            //TestEnvironment.AssertCollectionReferenceField<Settlement, Hero>(nameof(Settlement._heroesWithoutPartyCache));
            TestEnvironment.AssertField<Settlement, int>(nameof(Settlement._locatorNodeIndex), 1, defaultValue: settlement._locatorNodeIndex);

            // Certain MBLists aren't being registered correctly, waiting on a fix for certain collections with dynamic sync
            //TestEnvironment.AssertReferenceField<Settlement, MBList<Hero>>(nameof(Settlement._notablesCache));
            //TestEnvironment.AssertReferenceField<Settlement, MBList<MobileParty>>(nameof(Settlement._partiesCache));
            //TestEnvironment.AssertReferenceField<Settlement, MBList<float>>(nameof(Settlement._settlementWallSectionHitPointsRatioList));
            //TestEnvironment.AssertReferenceField<Settlement, MBList<SiegeEvent.SiegeEngineMissile>>(nameof(Settlement._siegeEngineMissiles));
            //TestEnvironment.AssertReferenceField<Settlement, List<Alley>>(nameof(Settlement.Alleys));

            //TestEnvironment.AssertReferenceField<Settlement, ItemRoster>(nameof(Settlement.Stash)); // readonly
        }

        [Fact]
        public void Server_Settlement_Properties()
        {
            Server.ObjectManager.TryGetObject(settlementId, out Settlement settlement);

            TestEnvironment.AssertReferenceProperty<Settlement, PartyBase>(nameof(Settlement.Party));
            TestEnvironment.AssertProperty<Settlement, int>(nameof(Settlement.BribePaid), 43);
            //TestEnvironment.AssertReferenceProperty<Settlement, SiegeEvent>(nameof(Settlement.SiegeEvent)); // Need SiegeEvent from constructor to be successful
            TestEnvironment.AssertProperty<Settlement, bool>(nameof(Settlement.IsActive), false, defaultValue: settlement.IsActive);
            TestEnvironment.AssertProperty<Settlement, bool>(nameof(Settlement.IsVisible), false, defaultValue: settlement.IsVisible);
            TestEnvironment.AssertProperty<Settlement, Settlement.SiegeState>(nameof(Settlement.CurrentSiegeState), Settlement.SiegeState.OnTheWalls);
            TestEnvironment.AssertProperty<Settlement, CampaignVec2>(nameof(Settlement.GatePosition), new CampaignVec2(new Vec2(1, 2), false), settlement.GatePosition);

            //TestEnvironment.AssertProperty<Settlement, float>(nameof(Settlement.NearbyLandThreatIntensity), 20f); // Expected: 235 Actual: 0
            TestEnvironment.AssertProperty<Settlement, float>(nameof(Settlement.NearbyNavalThreatIntensity), 235f);
            //TestEnvironment.AssertProperty<Settlement, float>(nameof(Settlement.NearbyLandAllyIntensity), 1f, defaultValue: settlement.NearbyLandAllyIntensity); // Expected: 1 Actual: 0
            TestEnvironment.AssertProperty<Settlement, float>(nameof(Settlement.NearbyNavalAllyIntensity), 10f);
        }
    }
}