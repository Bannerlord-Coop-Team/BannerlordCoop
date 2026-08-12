using HarmonyLib;
using System.Collections;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class MapEventRobustnessPatchesTests : MapEventTestBase
{
    public MapEventRobustnessPatchesTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void TroopUpgradeTracker_WhenNullAfterLoad_IsRepopulatedWithEveryInvolvedParty()
    {
        var context = CreateServerMapEvent();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));

            var trackerField = AccessTools.Field(typeof(MapEvent), "<TroopUpgradeTracker>k__BackingField");
            Assert.NotNull(trackerField);
            trackerField.SetValue(mapEvent, null);

            var restored = mapEvent.TroopUpgradeTracker;
            Assert.NotNull(restored);

            var mapEventPartiesField = AccessTools.Field(typeof(TroopUpgradeTracker), "_mapEventParties");
            Assert.NotNull(mapEventPartiesField);
            var mapEventParties = (IList)mapEventPartiesField.GetValue(restored)!;

            var involvedParties = new List<MapEventParty>();
            involvedParties.AddRange(mapEvent.AttackerSide.Parties);
            involvedParties.AddRange(mapEvent.DefenderSide.Parties);

            Assert.Equal(involvedParties.Count, mapEventParties.Count);
            foreach (var party in involvedParties)
            {
                Assert.Contains(party, mapEventParties.Cast<MapEventParty>());
            }
        }, MapEventDisabledMethods);
    }
}
