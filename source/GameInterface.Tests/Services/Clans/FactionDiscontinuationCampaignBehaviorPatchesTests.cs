using Common.Util;
using GameInterface.Configuration;
using GameInterface.Services.Clans.Patches;
using GameInterface.Tests.Bootstrap;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Clans;

[Collection(ModInformationRoleCollection.Name)]
public class FactionDiscontinuationCampaignBehaviorPatchesTests : IDisposable
{
    private readonly ModOptions previousOptions = ModConfigProvider.ModOptions;
    private readonly FactionDiscontinuationCampaignBehavior behavior = new();

    public FactionDiscontinuationCampaignBehaviorPatchesTests()
    {
        GameBootStrap.Initialize();
    }

    public void Dispose()
    {
        ModConfigProvider.ModOptions = previousOptions;
    }

    [Fact]
    public void KeepClans_ClanReleasedByKingdomDestruction_GetsNoTimer()
    {
        SetMode(ClansAfterKingdomDestructionMode.KeepClans);
        Clan clan = CreateIndependentClan();

        LeaveKingdom(clan, ChangeKingdomAction.ChangeKingdomActionDetail.LeaveByKingdomDestruction);

        Assert.False(behavior._independentClans.ContainsKey(clan));
    }

    [Theory]
    [InlineData(ClansAfterKingdomDestructionMode.Vanilla)]
    [InlineData(ClansAfterKingdomDestructionMode.KeepClans)]
    public void ClanLeavingExistingKingdom_GetsTimer(ClansAfterKingdomDestructionMode mode)
    {
        SetMode(mode);
        Clan clan = CreateIndependentClan();

        LeaveKingdom(clan, ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom);

        Assert.True(behavior._independentClans.ContainsKey(clan));
    }

    [Fact]
    public void KeepClans_PendingTimer_IsKeptWhenAnotherClanIsReleasedByKingdomDestruction()
    {
        Clan pendingClan = CreateIndependentClan();
        CampaignTime pendingTimer = CampaignTime.Days(3f);
        behavior._independentClans.Add(pendingClan, pendingTimer);
        SetMode(ClansAfterKingdomDestructionMode.KeepClans);

        LeaveKingdom(CreateIndependentClan(), ChangeKingdomAction.ChangeKingdomActionDetail.LeaveByKingdomDestruction);

        Assert.Single(behavior._independentClans);
        Assert.Equal(pendingTimer, behavior._independentClans[pendingClan]);
    }

    private void LeaveKingdom(Clan clan, ChangeKingdomAction.ChangeKingdomActionDetail detail)
    {
        FactionDiscontinuationCampaignBehaviorPatches.OnClanChangedKingdomPrefix(behavior, clan, null, null, detail);
    }

    private static void SetMode(ClansAfterKingdomDestructionMode mode)
    {
        ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData { ClansAfterKingdomDestruction = mode });
    }

    private static Clan CreateIndependentClan()
    {
        Clan clan = ObjectHelper.SkipConstructor<Clan>();
        clan._settlementsCache = new MBList<Settlement>();
        return clan;
    }
}
