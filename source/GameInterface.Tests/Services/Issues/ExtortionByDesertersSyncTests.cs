using Common.Messaging;
using Common.Network;
using GameInterface.AutoSync;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using Moq;
using System;
using System.Linq;
using System.Reflection;
using Xunit;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Tests.Services.Issues;

[Collection(nameof(CampaignCurrentCollection))]
public class ExtortionByDesertersSyncTests
{
    private static readonly (MethodBase Target, MethodInfo Prefix)[] DeferredPrefixes =
    {
        (AccessTools.Method(typeof(Quest), nameof(Quest.CreateDeserterParty)),
            AccessTools.Method(typeof(ExtortionByDesertersServerMethodsPatch), nameof(ExtortionByDesertersServerMethodsPatch.Prefix))),
        (AccessTools.Method(typeof(Quest), nameof(Quest.CreateDefenderParty)),
            AccessTools.Method(typeof(ExtortionByDesertersServerMethodsPatch), nameof(ExtortionByDesertersServerMethodsPatch.Prefix))),
        (AccessTools.Method(typeof(Quest), nameof(Quest.ReleaseDeserterParty)),
            AccessTools.Method(typeof(ExtortionByDesertersServerMethodsPatch), nameof(ExtortionByDesertersServerMethodsPatch.Prefix))),
        (AccessTools.Method(typeof(Quest), nameof(Quest.ApplyQuestResult)),
            AccessTools.Method(typeof(ExtortionByDesertersRewardPatches), nameof(ExtortionByDesertersRewardPatches.ApplyQuestResultPrefix))),
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Constructor_QueuesOnlyTheFourNativePrefixesWhenQuestsAreOn(bool questsOn)
    {
        var collector = new Mock<IAutoSyncPatchCollector>();
        using var sync = CreateSync(collector.Object, questsOn);

        foreach (var (target, prefix) in DeferredPrefixes)
        {
            collector.Verify(value => value.AddPrefix(target, prefix), questsOn ? Times.Once() : Times.Never());
        }
        collector.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Install_DefersNativePrefixesUntilCollectorDrainWithoutDuplicates(bool questsOn)
    {
        var harmony = new Harmony($"{nameof(ExtortionByDesertersSyncTests)}.{Guid.NewGuid():N}");
        using var collector = new AutoSyncPatchCollector(harmony);
        using var sync = CreateSync(collector, questsOn);

        try
        {
            new GameInterfacePatchInstaller(new TestQuestFeatureFlag(questsOn)).Install(harmony);

            foreach (var (target, _) in DeferredPrefixes)
            {
                Assert.Empty(Harmony.GetPatchInfo(target)?.Prefixes.Where(patch => patch.owner == harmony.Id) ?? Enumerable.Empty<Patch>());
            }

            collector.PatchAll();

            foreach (var (target, prefix) in DeferredPrefixes)
            {
                var installed = Harmony.GetPatchInfo(target)?.Prefixes.Where(patch => patch.owner == harmony.Id) ?? Enumerable.Empty<Patch>();
                if (questsOn)
                {
                    Assert.Equal(prefix, Assert.Single(installed).PatchMethod);
                }
                else
                {
                    Assert.Empty(installed);
                }
            }
        }
        finally
        {
            collector.UnpatchAll();
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static ExtortionByDesertersSync CreateSync(IAutoSyncPatchCollector collector, bool questsOn) =>
        new ExtortionByDesertersSync(
            Mock.Of<IMessageBroker>(), Mock.Of<INetwork>(), Mock.Of<IObjectManager>(),
            Mock.Of<IIssueResolver>(), Mock.Of<IIssueOwnerResolver>(), Mock.Of<IControllerIdProvider>(),
            Mock.Of<IPlayerManager>(), collector, new TestQuestFeatureFlag(questsOn));
}
