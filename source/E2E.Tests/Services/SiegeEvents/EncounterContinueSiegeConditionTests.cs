using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// The encounter menu's continue-siege condition runs on clients whose main party is not in the encountered map
/// event, where vanilla indexes the battle sides with the main party's Side of None.
/// </summary>
public class EncounterContinueSiegeConditionTests : IDisposable
{
    private const string ConditionName = "game_menu_town_besiege_continue_siege_on_condition";

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public EncounterContinueSiegeConditionTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ContinueSiegeCondition_MainPartyOutsideTheBattle_HidesTheOptionWithoutThrowing()
    {
        string partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Client.Call(() =>
        {
            var mapEvent = CreateMapEvent(out _, out var defenderSide);
            EncounterBattleThrough(Client.GetRegisteredObject<MobileParty>(partyId).Party, defenderSide);
            try
            {
                Assert.Same(mapEvent, PlayerEncounter.EncounteredBattle);
                Assert.Equal(BattleSideEnum.None, PartyBase.MainParty.Side);

                var (shown, args) = InvokeCondition();

                Assert.False(shown);
                Assert.Equal(GameMenuOption.LeaveType.Continue, args.optionLeaveType);
            }
            finally
            {
                ClearEncounter(Client.GetRegisteredObject<MobileParty>(partyId).Party);
            }
        });
    }

    [Fact]
    public void ContinueSiegeCondition_MainPartyLeadsTheAttack_RunsVanillaWithoutThrowing()
    {
        string partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Client.Call(() =>
        {
            var mapEvent = CreateMapEvent(out var attackerSide, out var defenderSide);
            attackerSide.LeaderParty = PartyBase.MainParty;
            using (new AllowedThread())
            {
                PartyBase.MainParty._mapEventSide = attackerSide;
            }
            EncounterBattleThrough(Client.GetRegisteredObject<MobileParty>(partyId).Party, defenderSide);
            try
            {
                Assert.Equal(BattleSideEnum.Attacker, PartyBase.MainParty.Side);
                Assert.Same(PartyBase.MainParty, mapEvent.GetLeaderParty(BattleSideEnum.Attacker));

                // With a side the prefix hands the condition to vanilla
                var shownByPrefix = false;
                var prefixArguments = new object[] { new MenuCallbackArgs((MenuContext)null, null), shownByPrefix };
                Assert.True((bool)AccessTools.Method(typeof(EncounterContinueSiegeConditionPatch), "Prefix")
                    .Invoke(null, prefixArguments));

                // The encountered party is no settlement, so vanilla reaches its leader check and declines
                var (shown, _) = InvokeCondition();

                Assert.False(shown);
            }
            finally
            {
                ClearEncounter(Client.GetRegisteredObject<MobileParty>(partyId).Party);
                using (new AllowedThread())
                {
                    PartyBase.MainParty._mapEventSide = null;
                }
            }
        });
    }

    private static MapEvent CreateMapEvent(out MapEventSide attackerSide, out MapEventSide defenderSide)
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        defenderSide = CreateSide(mapEvent, BattleSideEnum.Defender);
        attackerSide = CreateSide(mapEvent, BattleSideEnum.Attacker);
        mapEvent._sides = new[] { defenderSide, attackerSide };
        return mapEvent;
    }

    private static MapEventSide CreateSide(MapEvent mapEvent, BattleSideEnum missionSide)
    {
        var side = ObjectHelper.SkipConstructor<MapEventSide>();
        AccessTools.Field(typeof(MapEventSide), "_mapEvent").SetValue(side, mapEvent);
        side.MissionSide = missionSide;
        return side;
    }

    private static void EncounterBattleThrough(PartyBase encounteredParty, MapEventSide side)
    {
        using (new AllowedThread())
        {
            encounteredParty._mapEventSide = side;
        }

        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        encounter._encounteredParty = encounteredParty;
        Campaign.Current.PlayerEncounter = encounter;
    }

    private static void ClearEncounter(PartyBase encounteredParty)
    {
        Campaign.Current.PlayerEncounter = null;
        using (new AllowedThread())
        {
            encounteredParty._mapEventSide = null;
        }
    }

    private static (bool shown, MenuCallbackArgs args) InvokeCondition()
    {
        var args = new MenuCallbackArgs((MenuContext)null, null);
        var shown = (bool)AccessTools.Method(typeof(EncounterGameMenuBehavior), ConditionName)
            .Invoke(ObjectHelper.SkipConstructor<EncounterGameMenuBehavior>(), new object[] { args });
        return (shown, args);
    }
}
