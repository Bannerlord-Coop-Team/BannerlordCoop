using Common.Util;
using GameInterface.Services.CampaignService.Patches;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;
using Xunit;

namespace GameInterface.Tests.Services.CampaignService;

/// <summary>
/// v1.5 battle wreckages are off on every role, and every wreckage a save already holds is dropped
/// before the object lists are built, since id-less ones throw there and none of them would expire.
/// </summary>
public class BattleWreckageCoopTests
{
    [Fact]
    public void SavedWreckages_AreAllDroppedSoTheObjectListsInitialize()
    {
        var manager = new CampaignObjectManager();
        var idless = ObjectHelper.SkipConstructor<BattleWreckage>();
        var named = ObjectHelper.SkipConstructor<BattleWreckage>();
        using (new AllowedThread()) named.StringId = "wreckage_1";
        manager._wreckages.Add(idless);
        manager._wreckages.Add(named);

        // Baseline: without the removal the vanilla load path throws like the client dump.
        Assert.Throws<NullReferenceException>(() => manager.InitializeManagerObjectLists());

        Assert.Equal(2, SavedWreckageRemovalPatch.RemoveSavedWreckages(manager));
        manager.InitializeManagerObjectLists();
        Assert.Empty(manager.Wreckages);
    }
}
