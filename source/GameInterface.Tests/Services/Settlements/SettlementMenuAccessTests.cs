using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements;
using GameInterface.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Patches;
using HarmonyLib;
using Moq;
using ProtoBuf;
using System;
using Xunit;

namespace GameInterface.Tests.Services.Settlements;

[Collection(ModInformationRoleCollection.Name)]
public class SettlementMenuAccessTests
{
    private readonly SettlementMenuAccess access = new(Mock.Of<INetwork>(), Mock.Of<IObjectManager>());

    [Fact]
    public void OnlyFirstPlayerCanOpenSameMenuAtSameSettlement()
    {
        Assert.True(access.TryAcquire("town_ES1", "manage_garrison", "leader"));
        Assert.False(access.TryAcquire("town_ES1", "manage_garrison", "member"));
        Assert.Equal("leader", Assert.Single(access.GetOpenMenus()).HeroId);
    }

    [Fact]
    public void DifferentMenusAndSettlementsRemainAvailable()
    {
        Assert.True(access.TryAcquire("town_ES1", "manage_garrison", "leader"));
        Assert.True(access.TryAcquire("town_ES1", "open_stash", "member"));
        Assert.True(access.TryAcquire("town_EN1", "manage_garrison", "other"));
        Assert.Equal(3, access.GetOpenMenus().Length);
    }

    [Fact]
    public void ReleasingPlayerAllowsNextPlayerWithoutUnlockingOthers()
    {
        access.TryAcquire("town_ES1", "manage_garrison", "leader");
        access.TryAcquire("town_ES1", "open_stash", "member");

        Assert.False(access.Release("other"));
        Assert.True(access.Release("leader"));
        Assert.True(access.TryAcquire("town_ES1", "manage_garrison", "other"));
        Assert.True(access.IsInUse("town_ES1", "open_stash"));
    }

    [Fact]
    public void OpeningDifferentMenuReleasesPreviousMenuButFailedRequestDoesNot()
    {
        access.TryAcquire("town_ES1", "manage_garrison", "leader");
        access.TryAcquire("town_ES1", "open_stash", "member");
        Assert.False(access.TryAcquire("town_ES1", "open_stash", "leader"));
        Assert.True(access.IsInUse("town_ES1", "manage_garrison"));

        Assert.True(access.TryAcquire("town_ES1", "town_prison_manage_prisoners", "leader"));
        Assert.False(access.IsInUse("town_ES1", "manage_garrison"));
    }

    [Fact]
    public void OccupancySnapshotRoundTripsBothPlayersAndMenus()
    {
        access.TryAcquire("town_ES1", "manage_garrison", "leader");
        access.TryAcquire("town_ES1", "open_stash", "member");
        var snapshot = Serializer.DeepClone(new NetworkSettlementMenusChanged(access.GetOpenMenus()));

        Assert.Equal(access.GetOpenMenus(), snapshot.Menus);
        var request = Serializer.DeepClone(new RequestSettlementMenuAccess("town_ES1", "open_stash", true));
        var response = Serializer.DeepClone(new NetworkSettlementMenuAccess("town_ES1", "open_stash", false));
        Assert.True(request.Open);
        Assert.False(response.Granted);
        Assert.Equal(request.SettlementId, response.SettlementId);
        Assert.Equal(request.MenuId, response.MenuId);
    }

    [Theory]
    [InlineData(typeof(SettlementMenuAccessPatches))]
    [InlineData(typeof(DefaultSettlementAccessModelPatches))]
    public void MenuPatchesInstallAgainstCurrentGameAssemblies(Type patchType)
    {
        var harmony = new Harmony($"settlement-menu-test-{Guid.NewGuid()}");
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(patchType).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
