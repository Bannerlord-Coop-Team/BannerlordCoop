using Common.Util;
using GameInterface.Services.MapEvents;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;
using GameInterface.Tests.Utils;

namespace GameInterface.Tests.Services.MapEvents;

public class HideoutResultEncounterTests
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

        Assert.Equal(expected, HideoutResultEncounter.IsHoldingResults(encounter));
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.FieldBattle)]
    [InlineData(MapEvent.BattleTypes.Siege)]
    [InlineData(MapEvent.BattleTypes.Raid)]
    [InlineData(MapEvent.BattleTypes.SallyOut)]
    public void IsHoldingResults_OtherBattleType_IsFalse(MapEvent.BattleTypes battleType)
    {
        var encounter = CreateEncounter(battleType, PlayerEncounterState.CaptureHeroes);

        Assert.False(HideoutResultEncounter.IsHoldingResults(encounter));
    }

    [Fact]
    public void IsHoldingResults_EncounterWithoutMapEvent_IsFalse()
    {
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        encounter.EncounterState = PlayerEncounterState.CaptureHeroes;

        Assert.False(HideoutResultEncounter.IsHoldingResults(encounter));
    }

    [Fact]
    public void IsHoldingResults_NoEncounter_IsFalse()
    {
        Assert.False(HideoutResultEncounter.IsHoldingResults(null));
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
