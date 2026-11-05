using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameMenus.GameMenuInitializationHandlers;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// After a sally out the local encounter is closed while the player still stands in the besieged settlement, and the
/// request_meeting menus must still open.
/// </summary>
public class RequestMeetingMenuBackgroundTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public RequestMeetingMenuBackgroundTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void RequestMeetingInit_WithoutAnEncounter_UsesTheCurrentSettlementBackground()
    {
        Client.Call(() =>
        {
            var settlement = CreateSettlement("wait_besieged_town");
            using (new AllowedThread())
            {
                MobileParty.MainParty._currentSettlement = settlement;
            }
            try
            {
                Assert.Null(PlayerEncounter.EncounterSettlement);
                Assert.Same(settlement, Settlement.CurrentSettlement);

                var args = CreateMenuArgs();
                DefaultEncounter.game_menu_town_menu_request_meeting_on_init(args);

                Assert.Equal("wait_besieged_town", args.MenuContext.CurrentBackgroundMeshName);
            }
            finally
            {
                using (new AllowedThread())
                {
                    MobileParty.MainParty._currentSettlement = null;
                }
            }
        });
    }

    [Fact]
    public void RequestMeetingInit_WithAnEncounter_KeepsTheEncounterSettlementBackground()
    {
        Client.Call(() =>
        {
            var encounterSettlement = CreateSettlement("wait_encounter_town");
            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter.EncounterSettlementAux = encounterSettlement;
            Campaign.Current.PlayerEncounter = encounter;
            try
            {
                var args = CreateMenuArgs();
                DefaultEncounter.game_menu_town_menu_request_meeting_on_init(args);

                Assert.Equal("wait_encounter_town", args.MenuContext.CurrentBackgroundMeshName);
            }
            finally
            {
                Campaign.Current.PlayerEncounter = null;
            }
        });
    }

    [Theory]
    [InlineData("game_menu_request_meeting_town_leave_on_consequence")]
    [InlineData("game_menu_request_meeting_castle_leave_on_consequence")]
    public void ForgetIt_WithoutAnEncounter_SetsLeaveEncounterOnlyWhenOneIsOpen(string consequence)
    {
        var original = AccessTools.Method(typeof(EncounterGameMenuBehavior), consequence);
        Assert.Contains(Harmony.GetPatchInfo(original).Transpilers,
            patch => patch.PatchMethod.DeclaringType == typeof(RequestMeetingLeavePatch));

        var setLeaveEncounter = AccessTools.PropertySetter(typeof(PlayerEncounter), nameof(PlayerEncounter.LeaveEncounter));
        var instructions = PatchProcessor.GetOriginalInstructions(original);
        Assert.Contains(instructions, instruction => instruction.Calls(setLeaveEncounter));

        var rewritten = RequestMeetingLeavePatch.Transpiler(instructions).ToList();
        Assert.DoesNotContain(rewritten, instruction => instruction.Calls(setLeaveEncounter));

        Client.Call(() =>
        {
            Campaign.Current.PlayerEncounter = null;
            RequestMeetingLeavePatch.SetLeaveEncounterIfOpen(false);

            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter._leaveEncounter = true;
            Campaign.Current.PlayerEncounter = encounter;
            try
            {
                RequestMeetingLeavePatch.SetLeaveEncounterIfOpen(false);
                Assert.False(encounter._leaveEncounter);
            }
            finally
            {
                Campaign.Current.PlayerEncounter = null;
            }
        });
    }

    private static Settlement CreateSettlement(string waitMesh)
    {
        var town = ObjectHelper.SkipConstructor<Town>();
        town.WaitMeshName = waitMesh;
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        settlement.SettlementComponent = town;
        return settlement;
    }

    private static MenuCallbackArgs CreateMenuArgs()
        => new MenuCallbackArgs(ObjectHelper.SkipConstructor<MenuContext>(), null);
}
