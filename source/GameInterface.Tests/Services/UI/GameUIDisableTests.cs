using Common;
using Common.Util;
using GameInterface.Services.UI.Patches;
using TaleWorlds.CampaignSystem.GameState;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class GameUIDisableTests
{
    [Fact]
    public void KingdomState_IsAllowedOnClientAndServer()
    {
        var kingdomState = ObjectHelper.SkipConstructor<KingdomState>();
        bool originalIsServer = ModInformation.IsServer;

        try
        {
            ModInformation.IsServer = false;
            Assert.True(GameUIDisable.PushStatePatch(kingdomState));

            ModInformation.IsServer = true;
            Assert.True(GameUIDisable.PushStatePatch(kingdomState));
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
        }
    }
}
