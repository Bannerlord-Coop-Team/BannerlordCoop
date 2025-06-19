using E2E.Tests.Util;
using HarmonyLib;
using Newtonsoft.Json.Bson;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEventParties
{
    public class MapEventPartySyncTests : SyncTestBase
    {

        public MapEventPartySyncTests(ITestOutputHelper output) : base(output)
        {
            TestEnvironment.CreateRegisteredObject<MapEventParty>();
            TestEnvironment.CreateRegisteredObject<PartyBase>();
        }

        [Fact]
        public void Server_MapEventParty_Properties()
        {
            TestEnvironment.AssertProperty<MapEventParty, float>(nameof(MapEventParty.GainedInfluence), 5f);
            TestEnvironment.AssertProperty<MapEventParty, float>(nameof(MapEventParty.GainedRenown), 2f);
            TestEnvironment.AssertProperty<MapEventParty, int>(nameof(MapEventParty.GoldLost), 3);
            TestEnvironment.AssertProperty<MapEventParty, float>(nameof(MapEventParty.MoraleChange), 3f);
            TestEnvironment.AssertReferenceProperty<MapEventParty, PartyBase>(nameof(MapEventParty.Party));
            TestEnvironment.AssertProperty<MapEventParty, int>(nameof(MapEventParty.PlunderedGold), 3);
            
        }

        [Fact]
        public void Server_MapEventParty_Fields()
        {
            TestEnvironment.AssertProperty<MapEventParty, int>(nameof(MapEventParty._contributionToBattle), 3, defaultValue: 1);
            TestEnvironment.AssertProperty<MapEventParty, int>(nameof(MapEventParty._healthyManCountAtStart), 3, defaultValue: 1);

        }
    }
}
