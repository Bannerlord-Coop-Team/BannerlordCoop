using Autofac;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface;
using GameInterface.Services.UI.Notifications.Patches;
using Moq;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.DefaultNotificationsBehavior;

public class DefaultNotificationsBehaviorPatchesTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    public DefaultNotificationsBehaviorPatchesTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        ContainerProvider.Clear();
    }

    public void Dispose()
    {
        ContainerProvider.Clear();
        TestEnvironment.Dispose();
    }

    [Fact]
    public void Prefix_ActiveCoopContainer_SuppressesVanillaOnArmyCreatedNotification()
    {
        DefaultNotificationsCampaignBehavior instance = null;
        using var container = BuildContainerWithGameInterface();

        using (ContainerProvider.UseContainerThreadSafe(container))
        {
            bool runOriginal = DefaultNotificationsCampaignBehaviorPatches.OnArmyCreatedPrefix(
                ref instance,
                army: null);

            Assert.False(runOriginal);
        }
    }

    [Fact]
    public void Prefix_NoActiveContainer_AllowsVanillaOnArmyCreatedNotification()
    {
        DefaultNotificationsCampaignBehavior instance = null;
        bool runOriginal = DefaultNotificationsCampaignBehaviorPatches.OnArmyCreatedPrefix(
            ref instance,
            army: null);

        Assert.True(runOriginal);
    }

    [Fact]
    public void Prefix_OnServer_SuppressesVanilla()
    {
        Army army = null;
        Server.Call(() =>
        {
            army = GameObjectCreator.CreateInitializedObject<Army>();

            DefaultNotificationsCampaignBehavior instance = null;
            bool runOriginal = DefaultNotificationsCampaignBehaviorPatches.OnArmyCreatedPrefix(
                ref instance,
                army);

            Assert.False(runOriginal);
        });
    }

    [Fact]
    public void Prefix_AfterContainerClearedLikeDestroyContainerCore_AllowsVanilla()
    {
        DefaultNotificationsCampaignBehavior instance = null;
        using var container = BuildContainerWithGameInterface();

        using (ContainerProvider.UseContainerThreadSafe(container))
        {
            Assert.False(DefaultNotificationsCampaignBehaviorPatches.OnArmyCreatedPrefix(
            ref instance, army: null));
        }
        ContainerProvider.Clear();

        Assert.True(DefaultNotificationsCampaignBehaviorPatches.OnArmyCreatedPrefix(
            ref instance, army: null));
    }

    private static IContainer BuildContainerWithGameInterface()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new Mock<IGameInterface>().Object).As<IGameInterface>();
        return builder.Build();
    }
}
