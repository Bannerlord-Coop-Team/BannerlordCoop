using Common.Util;
using GameInterface.Services.SiegeEvents.Patches;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

public class SiegePreparationPromptPatchesTests
{
    [Theory]
    [InlineData(MapEvent.BattleTypes.Siege, BattleState.None, true)]
    [InlineData(MapEvent.BattleTypes.Siege, BattleState.DefenderPullBack, true)]
    [InlineData(MapEvent.BattleTypes.Siege, BattleState.AttackerVictory, false)]
    [InlineData(MapEvent.BattleTypes.Siege, BattleState.DefenderVictory, false)]
    [InlineData(MapEvent.BattleTypes.FieldBattle, BattleState.None, false)]
    public void InterruptedActiveAssault_RequiresUnfinishedSiegeBattle(
        MapEvent.BattleTypes battleType,
        BattleState battleState,
        bool expected)
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        mapEvent._mapEventType = battleType;
        mapEvent._battleState = battleState;

        Assert.Equal(
            expected,
            SiegePreparationPromptPatches.IsInterruptedActiveAssault(mapEvent));
    }
}
