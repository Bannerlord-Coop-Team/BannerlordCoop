using Common;
using Common.Util;
using GameInterface.Services.SiegeEvents.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
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

    [Fact]
    public void FinalizePrefix_WhenParticipantCaptureThrows_AllowsRetryAfterFinalizer()
    {
        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            var settlement = ObjectHelper.SkipConstructor<Settlement>();
            settlement.Party = ObjectHelper.SkipConstructor<PartyBase>();
            var siegeEvent = ObjectHelper.SkipConstructor<SiegeEvent>();
            AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegedSettlement))
                .SetValue(siegeEvent, settlement);
            AccessTools.Field(typeof(SiegeEvent), nameof(SiegeEvent.BesiegerCamp))
                .SetValue(siegeEvent, ObjectHelper.SkipConstructor<BesiegerCamp>());
            var prefix = AccessTools.Method(
                typeof(SiegePreparationPromptPatches),
                "FinalizeSiegeEventPrefix");
            var finalizer = AccessTools.Method(
                typeof(SiegePreparationPromptPatches),
                "FinalizeSiegeEventFinalizer");

            var firstCapture = new object[] { siegeEvent, null };
            prefix.Invoke(null, firstCapture);
            Assert.NotNull(firstCapture[1]);
            Assert.Null(finalizer.Invoke(null, new[] { siegeEvent, firstCapture[1], null }));

            var retryCapture = new object[] { siegeEvent, null };
            prefix.Invoke(null, retryCapture);
            Assert.NotNull(retryCapture[1]);
            Assert.Null(finalizer.Invoke(null, new[] { siegeEvent, retryCapture[1], null }));
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
