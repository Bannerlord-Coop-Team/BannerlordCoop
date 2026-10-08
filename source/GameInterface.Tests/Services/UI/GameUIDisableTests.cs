using Common;
using Common.Util;
using GameInterface.Services.UI.Patches;
using GameInterface.Tests;
using TaleWorlds.CampaignSystem.GameState;
using Xunit;

namespace GameInterface.Tests.Services.UI;

[Collection(ModInformationRoleCollection.Name)]
public class GameUIDisableTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void QuestsState_IsAllowedOnClientOnly(bool isServer, bool allowed)
    {
        var questsState = ObjectHelper.SkipConstructor<QuestsState>();
        bool originalIsServer = ModInformation.IsServer;

        try
        {
            ModInformation.IsServer = isServer;
            Assert.Equal(allowed, GameUIDisable.PushStatePatch(questsState));
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
        }
    }

    [Fact]
    public void KingdomState_IsAllowedOnClientOnly()
    {
        var kingdomState = ObjectHelper.SkipConstructor<KingdomState>();
        bool originalIsServer = ModInformation.IsServer;

        try
        {
            ModInformation.IsServer = false;
            Assert.True(GameUIDisable.PushStatePatch(kingdomState));

            ModInformation.IsServer = true;
            Assert.False(GameUIDisable.PushStatePatch(kingdomState));
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
        }
    }
}
