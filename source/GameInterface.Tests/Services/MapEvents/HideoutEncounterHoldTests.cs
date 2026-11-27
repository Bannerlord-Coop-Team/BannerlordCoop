using Common.Util;
using GameInterface.Services.MapEvents;
using HarmonyLib;
using SandBox.Missions.MissionLogics.Hideout;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using GameInterface.Tests.Utils;

namespace GameInterface.Tests.Services.MapEvents;

public class HideoutEncounterHoldTests
{
    [Theory]
    [InlineData(PlayerEncounterState.Begin, false)]
    [InlineData(PlayerEncounterState.Wait, false)]
    [InlineData(PlayerEncounterState.PrepareResults, false)]
    [InlineData(PlayerEncounterState.ApplyResults, false)]
    [InlineData(PlayerEncounterState.PlayerVictory, false)]
    [InlineData(PlayerEncounterState.PlayerTotalDefeat, false)]
    [InlineData(PlayerEncounterState.CaptureHeroes, true)]
    [InlineData(PlayerEncounterState.FreeHeroes, true)]
    [InlineData(PlayerEncounterState.LootParty, true)]
    [InlineData(PlayerEncounterState.LootInventory, true)]
    [InlineData(PlayerEncounterState.LootShips, true)]
    [InlineData(PlayerEncounterState.End, true)]
    public void IsHoldingResults_HideoutBattle_OnlyInResultStates(PlayerEncounterState state, bool expected)
    {
        var encounter = CreateEncounter(MapEvent.BattleTypes.Hideout, state);

        Assert.Equal(expected, HideoutEncounterHold.IsHoldingResults(encounter));
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.FieldBattle)]
    [InlineData(MapEvent.BattleTypes.Siege)]
    [InlineData(MapEvent.BattleTypes.Raid)]
    [InlineData(MapEvent.BattleTypes.SallyOut)]
    public void IsHoldingResults_OtherBattleType_IsFalse(MapEvent.BattleTypes battleType)
    {
        var encounter = CreateEncounter(battleType, PlayerEncounterState.CaptureHeroes);

        Assert.False(HideoutEncounterHold.IsHoldingResults(encounter));
    }

    [Fact]
    public void IsHoldingResults_EncounterWithoutMapEvent_IsFalse()
    {
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        encounter.EncounterState = PlayerEncounterState.CaptureHeroes;

        Assert.False(HideoutEncounterHold.IsHoldingResults(encounter));
    }

    [Fact]
    public void IsHoldingResults_NoEncounter_IsFalse()
    {
        Assert.False(HideoutEncounterHold.IsHoldingResults(null));
    }

    [Theory]
    [InlineData("assault", MapEvent.BattleTypes.Hideout, false, true, true)]
    [InlineData("ambush", MapEvent.BattleTypes.Hideout, false, true, true)]
    [InlineData("other", MapEvent.BattleTypes.Hideout, false, true, false)]
    [InlineData(null, MapEvent.BattleTypes.Hideout, false, true, false)]
    [InlineData("assault", MapEvent.BattleTypes.FieldBattle, false, true, false)]
    [InlineData("assault", MapEvent.BattleTypes.Hideout, true, true, false)]
    [InlineData("assault", MapEvent.BattleTypes.Hideout, false, false, false)]
    public void IsFightingInMission_OnlyWhileANativeHideoutMissionFightsThePartysBattle(
        string? controller, MapEvent.BattleTypes battleType, bool finalized, bool partyInBattle, bool expected)
    {
        var encounter = CreateEncounter(battleType, PlayerEncounterState.Begin);
        if (finalized) encounter._mapEvent._state = MapEventState.WaitingRemoval;
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Party = ObjectHelper.SkipConstructor<PartyBase>();
        if (partyInBattle)
            party.Party._mapEventSide = new MapEventSide(encounter._mapEvent, BattleSideEnum.Attacker, party.Party);

        Assert.Equal(expected, HideoutEncounterHold.IsFightingInMission(encounter, party, CreateMission(controller)));
    }

    private static Mission? CreateMission(string? controller)
    {
        if (controller == null) return null;

        var mission = ObjectHelper.SkipConstructor<Mission>();
        // TaleWorlds.MountAndBlade isn't publicized here, so the getter-only list needs its backing field.
        var behaviors = new List<MissionBehavior>();
        AccessTools.Field(typeof(Mission), "<MissionBehaviors>k__BackingField").SetValue(mission, behaviors);
        if (controller == "assault") behaviors.Add(ObjectHelper.SkipConstructor<HideoutMissionController>());
        if (controller == "ambush") behaviors.Add(ObjectHelper.SkipConstructor<HideoutAmbushMissionController>());
        return mission;
    }

    private static PlayerEncounter CreateEncounter(MapEvent.BattleTypes battleType, PlayerEncounterState state)
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        mapEvent.SetBattleType(battleType);
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        encounter._mapEvent = mapEvent;
        encounter.EncounterState = state;
        return encounter;
    }
}
