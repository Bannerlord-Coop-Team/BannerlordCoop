using Common;
using Common.Util;
using GameInterface.Services.CampaignService.Patches;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Tests;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Xunit;

namespace GameInterface.Tests.Services.CampaignService;

/// <summary>
/// Battle wreckages are off on every role: the co-op StringId gate leaves a server-made wreckage
/// without an id, which throws in AddWreckage after it is already in the saved list. Saves that already
/// hold such wreckages are repaired before the object lists are built.
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class BattleWreckageCoopTests : IDisposable
{
    private readonly bool wasServer = ModInformation.IsServer;

    public void Dispose() => ModInformation.IsServer = wasServer;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RegisterEvents_IsSkippedOnEveryRole(bool isServer)
    {
        ModInformation.IsServer = isServer;

        Assert.False(BattleWreckageCampaignBehaviorPatches.RegisterEventsPrefix());
    }

    [Fact]
    public void OtherBehaviors_StayServerOnly()
    {
        ModInformation.IsServer = true;
        Assert.True(HeroDailyXpCampaignBehaviorPatches.RegisterEventsPrefix());
        Assert.True(EmptyClanPartiesCampaignBehaviorPatches.RegisterEventsPrefix());
        Assert.True(PartyConfigurationCampaignBehaviorPatches.RegisterEventsPrefix());

        ModInformation.IsServer = false;
        Assert.False(HeroDailyXpCampaignBehaviorPatches.RegisterEventsPrefix());
        Assert.False(EmptyClanPartiesCampaignBehaviorPatches.RegisterEventsPrefix());
        Assert.False(PartyConfigurationCampaignBehaviorPatches.RegisterEventsPrefix());
    }

    [Fact]
    public void IdlessWreckage_IsDroppedSoTheObjectListsInitialize()
    {
        var manager = new CampaignObjectManager();
        var idless = ObjectHelper.SkipConstructor<BattleWreckage>();
        var named = ObjectHelper.SkipConstructor<BattleWreckage>();
        using (new AllowedThread()) named.StringId = "wreckage_1";
        manager._wreckages.Add(idless);
        manager._wreckages.Add(named);

        // Baseline: without the repair the vanilla load path throws like the client dump.
        Assert.Throws<NullReferenceException>(() => manager.InitializeManagerObjectLists());

        Assert.Equal(1, IdlessWreckageLoadRepairPatch.RemoveIdlessWreckages(manager));
        manager.InitializeManagerObjectLists();
        Assert.Same(named, Assert.Single(manager.Wreckages));
    }

    [Fact]
    public void Repair_WithOnlyNamedWreckages_RemovesNothing()
    {
        var manager = new CampaignObjectManager();
        var named = ObjectHelper.SkipConstructor<BattleWreckage>();
        using (new AllowedThread()) named.StringId = "wreckage_1";
        manager._wreckages.Add(named);

        Assert.Equal(0, IdlessWreckageLoadRepairPatch.RemoveIdlessWreckages(manager));
        Assert.Same(named, Assert.Single(manager.Wreckages));
    }

    [Fact]
    public void Repair_DropsNullEntries()
    {
        var manager = new CampaignObjectManager();
        var named = ObjectHelper.SkipConstructor<BattleWreckage>();
        using (new AllowedThread()) named.StringId = "wreckage_1";
        manager._wreckages.Add(null);
        manager._wreckages.Add(named);

        Assert.Equal(1, IdlessWreckageLoadRepairPatch.RemoveIdlessWreckages(manager));
        Assert.Same(named, Assert.Single(manager.Wreckages));
    }

    [Fact]
    public void Repair_WithNoWreckageList_RemovesNothing()
    {
        var manager = new CampaignObjectManager();
        manager._wreckages = null;

        Assert.Equal(0, IdlessWreckageLoadRepairPatch.RemoveIdlessWreckages(manager));
    }

    [Fact]
    public void BothPatches_ResolveTheirTargetsAndPatch()
    {
        var harmony = new Harmony(nameof(BothPatches_ResolveTheirTargetsAndPatch));
        try
        {
            var disabled = harmony.CreateClassProcessor(typeof(BattleWreckageCampaignBehaviorPatches)).Patch();
            var repaired = harmony.CreateClassProcessor(typeof(IdlessWreckageLoadRepairPatch)).Patch();

            Assert.Contains(disabled, method => method.Name.Contains(nameof(BattleWreckageCampaignBehavior.RegisterEvents)));
            Assert.Contains(repaired, method => method.Name.Contains(nameof(CampaignObjectManager.InitializeOnLoad)));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
