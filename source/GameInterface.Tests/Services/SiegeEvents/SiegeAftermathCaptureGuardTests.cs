using Common.Util;
using GameInterface.Services.SiegeEvents.Patches;
using GameInterface.Tests.Utils;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

public class SiegeAftermathCaptureGuardTests
{
    /// <summary>
    /// v1.5 resolves an attacker win outside the walls as a broken siege
    /// (SiegeOutsideEventComponent reports it with isWin false), so it is not a capture.
    /// </summary>
    [Fact]
    public void SiegeOutsideAttackerVictory_IsNotACapture()
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        mapEvent.SetBattleType(MapEvent.BattleTypes.SiegeOutside);
        mapEvent.SetMapEventSettlement(ObjectHelper.SkipConstructor<Settlement>());
        mapEvent._battleState = BattleState.AttackerVictory;

        Assert.False(SiegeAftermathPatches.TryGetPlayerCaptureLeader(mapEvent, out var leaderParty, out var settlement));
        Assert.Null(leaderParty);
        Assert.Null(settlement);
    }
}
