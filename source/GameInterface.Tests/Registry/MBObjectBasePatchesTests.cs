using Autofac;
using Common;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Registry.Patches;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;
using Issue = TaleWorlds.CampaignSystem.Issues.CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue;

namespace GameInterface.Tests.Registry;

[Collection(ModInformationRoleCollection.Name)]
public class MBObjectBasePatchesTests
{
    private static readonly MethodInfo StringIdPrefix = AccessTools.Method(typeof(MBObjectBasePatches), "StringIdPrefix");

    // The running server does not allow originals, IssueManager still has to name the issues it creates
    [Fact]
    public void TheServerNamesTheIssuesItCreates()
    {
        Assert.True(RunPrefix(isServer: true, ObjectHelper.SkipConstructor<Issue>()));
    }

    [Fact]
    public void AClientDoesNotNameAnIssueOnItsOwn()
    {
        Assert.False(RunPrefix(isServer: false, ObjectHelper.SkipConstructor<Issue>()));
    }

    [Fact]
    public void TheServerStillDoesNotRenameOtherObjects()
    {
        Assert.False(RunPrefix(isServer: true, ObjectHelper.SkipConstructor<ItemCategory>()));
    }

    private static bool RunPrefix(bool isServer, MBObjectBase instance)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new DenyOriginalsPolicy()).As<ISyncPolicy>();
        using var container = builder.Build();

        bool hadPreviousContainer = ContainerProvider.TryGetContainer(out var previousContainer);
        bool originalIsServer = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = isServer;

            using (ContainerProvider.UseContainerThreadSafe(container))
            {
                return (bool)StringIdPrefix.Invoke(null, new object[] { instance, "issue_1" });
            }
        }
        finally
        {
            ModInformation.IsServer = originalIsServer;

            if (hadPreviousContainer)
            {
                ContainerProvider.SetContainer(previousContainer);
            }
            else
            {
                ContainerProvider.Clear();
            }
        }
    }

    private sealed class DenyOriginalsPolicy : ISyncPolicy
    {
        public bool AllowOriginal() => false;
    }
}
