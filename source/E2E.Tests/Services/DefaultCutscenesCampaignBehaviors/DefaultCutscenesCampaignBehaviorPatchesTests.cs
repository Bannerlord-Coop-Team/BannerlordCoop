using Autofac;
using GameInterface;
using GameInterface.Services.Clans.Patches;
using Moq;
using TaleWorlds.CampaignSystem.Actions;

namespace E2E.Tests.Services.DefaultCutscenesCampaignBehaviors;

public class DefaultCutscenesCampaignBehaviorPatchesTests : IDisposable
{
    public DefaultCutscenesCampaignBehaviorPatchesTests()
    {
        ContainerProvider.Clear();
    }

    public void Dispose()
    {
        ContainerProvider.Clear();
    }

    [Fact]
    public void Prefix_ActiveCoopContainer_SuppressesVanillaJoinKingdomScene()
    {
        using var container = BuildContainerWithGameInterface();

        using (ContainerProvider.UseContainerThreadSafe(container))
        {
            bool runOriginal = ClanKingdomPatches.Prefix(
                clan: null,
                oldKingdom: null,
                newKingdom: null,
                detail: ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom,
                showNotification: true);

            Assert.False(runOriginal);
        }
    }

    [Fact]
    public void Prefix_NoActiveContainer_AllowsVanillaJoinKingdomScene()
    {
        bool runOriginal = ClanKingdomPatches.Prefix(
            clan: null,
            oldKingdom: null,
            newKingdom: null,
            detail: ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom,
            showNotification: true);

        Assert.True(runOriginal);
    }

    [Fact]
    public void Prefix_AfterContainerClearedLikeDestroyContainerCore_AllowsVanilla()
    {
        using var container = BuildContainerWithGameInterface();

        using (ContainerProvider.UseContainerThreadSafe(container))
        {
            Assert.False(ClanKingdomPatches.Prefix(
                null, null, null, ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, true));
        }
        ContainerProvider.Clear();

        Assert.True(ClanKingdomPatches.Prefix(
            null, null, null, ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, true));
    }

    private static IContainer BuildContainerWithGameInterface()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new Mock<IGameInterface>().Object).As<IGameInterface>();
        return builder.Build();
    }
}