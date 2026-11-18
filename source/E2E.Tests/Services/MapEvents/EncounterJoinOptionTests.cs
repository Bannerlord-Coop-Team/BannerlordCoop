using Common.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// The join encounter options ("Help the attackers" / "Help the defenders") can be refreshed on a client while
/// its encounter is still being set up and has no encountered party yet. Native and coop both read
/// <c>PlayerEncounter.EncounteredBattle</c> there, which dereferences that party, so the refresh must refuse the
/// option instead of throwing out of the menu.
/// </summary>
public class EncounterJoinOptionTests : MapEventTestBase
{
    private const string HelpAttackersCondition = "game_menu_join_encounter_help_attackers_on_condition";
    private const string HelpDefendersCondition = "game_menu_join_encounter_help_defenders_on_condition";

    public EncounterJoinOptionTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(HelpAttackersCondition)]
    [InlineData(HelpDefendersCondition)]
    public void JoinCondition_BeforeEncounteredPartyIsSet_IsRefusedInsteadOfThrowing(string conditionName)
    {
        var client = Clients.First();
        SetMockPlayerEncounter(client);

        client.Call(() =>
        {
            Assert.Null(PlayerEncounter.Current._encounteredParty);

            var (shown, args) = InvokeJoinCondition(conditionName);

            Assert.False(shown);
            Assert.False(args.IsEnabled);
            Assert.Equal("Waiting for the server to set up the battle.", args.Tooltip?.ToString());
        }, MapEventDisabledMethods);
    }

    /// <summary>A client's map event sides and their leader parties arrive after the battle itself, and vanilla
    /// reads the leader of the side not being joined.</summary>
    [Theory]
    [InlineData(HelpAttackersCondition, BattleSideEnum.Defender)]
    [InlineData(HelpDefendersCondition, BattleSideEnum.Attacker)]
    public void JoinCondition_BeforeLeaderPartyArrives_IsRefusedInsteadOfThrowing(string conditionName, BattleSideEnum unsetSide)
    {
        var client = Clients.First();
        var ctx = CreateServerMapEvent();
        SetMockPlayerEncounter(client, ctx.AttackerPartyId, ctx.MapEventId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(ctx.MapEventId, out var mapEvent));
            Assert.Same(mapEvent, PlayerEncounter.EncounteredBattle);
            using (new AllowedThread())
                mapEvent.GetMapEventSide(unsetSide).LeaderParty = null;

            var (shown, args) = InvokeJoinCondition(conditionName);

            Assert.False(shown);
            Assert.False(args.IsEnabled);
            Assert.Equal("Waiting for the server to set up the battle.", args.Tooltip?.ToString());
        }, MapEventDisabledMethods);
    }

    /// <summary>Runs the real (Harmony patched) join condition in the calling instance's scope.</summary>
    private static (bool shown, MenuCallbackArgs args) InvokeJoinCondition(string conditionName)
    {
        var behavior = new EncounterGameMenuBehavior();
        var args = new MenuCallbackArgs((MenuContext)null, null);

        var method = AccessTools.Method(typeof(EncounterGameMenuBehavior), conditionName);
        Assert.NotNull(method);

        var shown = (bool)method.Invoke(behavior, new object[] { args });
        return (shown, args);
    }
}
