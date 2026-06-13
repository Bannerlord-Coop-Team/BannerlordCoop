using E2E.Tests.Util;
using HarmonyLib;
using Newtonsoft.Json.Bson;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEventParties
{
    public class MapEventPartySyncTests : SyncTestBase
    {
        string MepId;
        public MapEventPartySyncTests(ITestOutputHelper output) : base(output)
        {
            MepId = TestEnvironment.CreateRegisteredObject<MapEventParty>();
            TestEnvironment.CreateRegisteredObject<PartyBase>();
        }

        [Fact]
        public void Server_MapEventParty_Properties()
        {
            Server.ObjectManager.TryGetObject(MepId, out MapEventParty eventParty);
            eventParty.Party = null;
            TestEnvironment.AssertProperty<MapEventParty, ExplainedNumber>(nameof(MapEventParty.GainedInfluenceExplained), new ExplainedNumber(5f), defaultValue: eventParty.GainedInfluenceExplained);
            TestEnvironment.AssertProperty<MapEventParty, ExplainedNumber>(nameof(MapEventParty.GainedRenownExplained), new ExplainedNumber(2f), defaultValue: eventParty.GainedRenownExplained);
            TestEnvironment.AssertProperty<MapEventParty, int>(nameof(MapEventParty.GoldLost), 3);
            TestEnvironment.AssertReferenceProperty<MapEventParty, PartyBase>(nameof(MapEventParty.Party));
            TestEnvironment.AssertProperty<MapEventParty, int>(nameof(MapEventParty.PlunderedGold), 3);
            
        }

        [Fact]
        public void Server_MapEventParty_Fields()
        {
            Server.ObjectManager.TryGetObject(MepId, out MapEventParty eventParty);
            //TestEnvironment.AssertField<MapEventParty, int>(nameof(MapEventParty._contributionToBattle), 3, defaultValue: eventParty._contributionToBattle);
            TestEnvironment.AssertField<MapEventParty, int>(nameof(MapEventParty._healthyManCountAtStart), 3, defaultValue: eventParty._healthyManCountAtStart);
        }
    }
}