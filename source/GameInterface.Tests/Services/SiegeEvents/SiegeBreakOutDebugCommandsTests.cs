#if DEBUG
using Common;
using Common.Commands;
using Common.Util;
using GameInterface.Services.SiegeEvents.Commands;
using GameInterface.Tests.Bootstrap;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

[Collection(nameof(CampaignCurrentCollection))]
public sealed class SiegeBreakOutDebugCommandsTests : IDisposable
{
    private readonly Harmony harmony = new("Coop.Tests.BreakOutCommands");
    private readonly MobileParty previousParty;
    private readonly PlayerEncounter previousEncounter;
    private readonly bool previousServer;
    private readonly MapState map;
    private readonly EncounterGameMenuBehavior behavior;
    private readonly bool previousPort;
    private static int consequenceCalls;
    private static bool mayJoin;

    public SiegeBreakOutDebugCommandsTests()
    {
        GameBootStrap.Initialize();
        previousParty = Campaign.Current.MainParty;
        previousEncounter = Campaign.Current.PlayerEncounter;
        previousServer = ModInformation.IsServer;
        ModInformation.IsServer = false;
        behavior = Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>();
        Assert.NotNull(behavior);
        previousPort = behavior._isBreakingOutFromPort;
        behavior._isBreakingOutFromPort = false;
        consequenceCalls = 0;
        mayJoin = true;
        foreach (string method in new[] { "game_menu_encounter_interrupted_siege_preparations_join_defend_on_consequence", "menu_defender_siege_break_out_from_gate_on_consequence", "break_out_menu_accept_on_consequence", "break_out_debrief_continue_on_consequence" })
            harmony.Patch(AccessTools.Method(typeof(EncounterGameMenuBehavior), method),
                prefix: new HarmonyMethod(typeof(SiegeBreakOutDebugCommandsTests), nameof(RecordConsequence)) { priority = Priority.First });
        harmony.Patch(AccessTools.Method(typeof(SiegeEvent), nameof(SiegeEvent.CanPartyJoinSide)),
            prefix: new HarmonyMethod(typeof(SiegeBreakOutDebugCommandsTests), nameof(CanJoin)));
        map = Game.Current.GameStateManager.CreateState<MapState>();
        Game.Current.GameStateManager._gameStates.Add(map);
        map._menuContext = Game.Current.ObjectManager.CreateObject<MenuContext>();
        map._menuContext.GameMenu = ObjectHelper.SkipConstructor<GameMenu>();
    }

    [Theory]
    [InlineData("town_ES1", "join")]
    [InlineData("castle_ES1", "join")]
    [InlineData("castle_B1", "join")]
    [InlineData("town_ES1", "gate")]
    [InlineData("castle_ES1", "gate")]
    [InlineData("castle_B1", "gate")]
    [InlineData("town_ES1", "accept")]
    [InlineData("castle_ES1", "accept")]
    [InlineData("castle_B1", "accept")]
    public void CurrentFortification_InvokesNativeMenuConsequence(string id, string action)
    {
        Prepare(id, action);
        Assert.Null(MobileParty.MainParty.BesiegedSettlement);
        Assert.True(Command(action).ProcessCommand(null).Succeeded);
        Assert.Equal(1, consequenceCalls);
    }

    [Theory]
    [InlineData("join", "wrong-encounter")]
    [InlineData("gate", "wrong-encounter")]
    [InlineData("accept", "wrong-encounter")]
    [InlineData("join", "wrong-menu")]
    [InlineData("gate", "wrong-menu")]
    [InlineData("accept", "wrong-menu")]
    [InlineData("join", "cannot-join")]
    [InlineData("accept", "port")]
    public void InvalidState_RefusesWithoutInvokingConsequence(string action, string invalid)
    {
        Prepare("castle_ES1", action);
        if (invalid == "wrong-encounter") PlayerEncounter.Current.EncounterSettlementAux = ObjectHelper.SkipConstructor<Settlement>();
        if (invalid == "wrong-menu") map._menuContext.GameMenu.StringId = "encounter";
        if (invalid == "cannot-join") mayJoin = false;
        if (invalid == "port") behavior._isBreakingOutFromPort = true;
        Assert.False(Command(action).ProcessCommand(null).Succeeded);
        Assert.Equal(0, consequenceCalls);
    }

    [Theory]
    [InlineData("break_out_debrief_menu", false, true)]
    [InlineData("break_out_menu", false, false)]
    [InlineData("break_out_debrief_menu", true, false)]
    public void ContinueAfterDeparture_RequiresLandDebrief(string menu, bool port, bool succeeds)
    {
        Prepare("castle_ES1", "accept");
        MobileParty.MainParty._currentSettlement = null;
        Campaign.Current.PlayerEncounter = null;
        map._menuContext.GameMenu.StringId = menu;
        behavior._isBreakingOutFromPort = port;
        Assert.Null(PlayerSiege.PlayerSiegeEvent);
        Assert.Equal(succeeds, new SiegeBreakOutDebugCommands.ContinueCoopCommand().ProcessCommand(null).Succeeded);
        Assert.Equal(succeeds ? 1 : 0, consequenceCalls);
    }

    private void Prepare(string id, string action)
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        settlement.StringId = id;
        settlement.Party = ObjectHelper.SkipConstructor<PartyBase>();
        settlement.Party.Settlement = settlement;
        settlement.Party.ItemRoster = new ItemRoster();
        settlement.Town = ObjectHelper.SkipConstructor<Town>();
        settlement.Town._isCastle = id.StartsWith("castle_");
        settlement.Town.Owner = settlement.Party;
        settlement.SettlementComponent = settlement.Town;
        settlement.SiegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
        // The native field is readonly, so constructor-free fixture setup needs reflection.
        AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement)).SetValue(settlement.SiegeEvent, settlement);
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Party = ObjectHelper.SkipConstructor<PartyBase>();
        party.Party.MobileParty = party;
        party._currentSettlement = settlement;
        Campaign.Current.MainParty = party;
        Campaign.Current.PlayerEncounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        PlayerEncounter.Current.EncounterSettlementAux = settlement;
        map._menuContext.GameMenu.StringId = action == "join" ? "encounter_interrupted_siege_preparations" : action == "gate" ? "menu_siege_strategies" : "break_out_menu";
    }

    private static ICoopCommand Command(string action) => action == "join"
        ? new SiegeBreakOutDebugCommands.JoinDefenseCoopCommand()
        : action == "gate" ? new SiegeBreakOutDebugCommands.GateCoopCommand() : new SiegeBreakOutDebugCommands.AcceptCoopCommand();

    private static bool RecordConsequence() { consequenceCalls++; return false; }
    private static bool CanJoin(ref bool __result) { __result = mayJoin; return false; }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        Game.Current.GameStateManager._gameStates.Remove(map);
        behavior._isBreakingOutFromPort = previousPort;
        Campaign.Current.MainParty = previousParty;
        Campaign.Current.PlayerEncounter = previousEncounter;
        ModInformation.IsServer = previousServer;
    }
}
#endif
