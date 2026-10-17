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

    [Fact]
    public void QuestsState_IsAllowedOnClientOnly()
    {
        var questsState = ObjectHelper.SkipConstructor<QuestsState>();
        bool originalIsServer = ModInformation.IsServer;

        try
        {
            ModInformation.IsServer = false;
            Assert.True(GameUIDisable.PushStatePatch(questsState));

            ModInformation.IsServer = true;
            Assert.False(GameUIDisable.PushStatePatch(questsState));
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;
        }
    }
}
