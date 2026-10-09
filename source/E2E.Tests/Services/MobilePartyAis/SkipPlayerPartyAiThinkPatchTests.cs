using E2E.Tests.Services.MapEvents;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobilePartyAis;

/// <summary>
/// Verifies which parties the server lets run the vanilla hourly AI decision tick.
/// </summary>
public class SkipPlayerPartyAiThinkPatchTests : MapEventTestBase
{
    public SkipPlayerPartyAiThinkPatchTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void AiPartyInAiOnlyMapEvent_ThinkRuns()
    {
        var context = CreateServerMapEvent();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(context.AttackerPartyId, out var attacker));

            Assert.True(InvokeThinkPrefix(attacker));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void AiPartyInMapEventWithPlayerParty_ThinkIsSkipped()
    {
        var context = CreateServerMapEvent();
        var (_, playerPartyId) = CreatePlayerHeroParty("player");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(context.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(context.AttackerPartyId, out var attacker));

            playerParty.Party._mapEventSide = mapEvent.DefenderSide;
            mapEvent.DefenderSide._battleParties.Add(new MapEventParty(playerParty.Party));

            Assert.False(InvokeThinkPrefix(attacker));
        }, MapEventDisabledMethods);
    }

    private static bool InvokeThinkPrefix(MobileParty party)
    {
        var patchType = AccessTools.TypeByName("GameInterface.Services.MobilePartyAIs.Patches.SkipPlayerPartyAiThinkPatch");
        var prefix = AccessTools.Method(patchType, "Prefix");
        Assert.NotNull(prefix);

        return (bool)prefix.Invoke(null, new object[] { party })!;
    }
}
