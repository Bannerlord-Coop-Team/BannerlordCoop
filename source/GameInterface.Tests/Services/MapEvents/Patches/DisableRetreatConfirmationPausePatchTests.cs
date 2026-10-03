using GameInterface.Services.MapEvents.Patches.Disable;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents.Patches;

public class DisableRetreatConfirmationPausePatchTests
{
    [Fact]
    public void IsRetreatDisabled_NavalBattle_BlocksRetreat()
    {
        Assert.True(DisableRetreatConfirmationPausePatch.IsRetreatDisabled(CreateMapEvent(isOnLand: false)));
    }

    [Fact]
    public void IsRetreatDisabled_LandBattle_KeepsRetreat()
    {
        Assert.False(DisableRetreatConfirmationPausePatch.IsRetreatDisabled(CreateMapEvent(isOnLand: true)));
    }

    private static MapEvent CreateMapEvent(bool isOnLand)
    {
        var mapEvent = (MapEvent)RuntimeHelpers.GetUninitializedObject(typeof(MapEvent));
        mapEvent.Position = new CampaignVec2(new Vec2(10f, 10f), isOnLand);
        return mapEvent;
    }
}
