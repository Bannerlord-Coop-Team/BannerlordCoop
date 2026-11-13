using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;
using GameInterface.Services.Issues.Framework.Dispatch;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Xunit;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Tests.Services.Issues;

[Collection(nameof(CampaignCurrentCollection))]
public class ExtortionByDesertersPatchTests
{
    [Fact]
    public void RewardDialogBranchesHaveIndependentOutcomePrefixes()
    {
        var harmony = new Harmony($"{nameof(RewardDialogBranchesHaveIndependentOutcomePrefixes)}.{Guid.NewGuid():N}");
        try
        {
            harmony.PatchCategory(typeof(GameInterface).Assembly, GameInterface.HARMONY_QUESTS_CATEGORY);

            var branchMethods = new[]
            {
                AccessTools.Method(typeof(Quest), "<get_QuestCompletionDialogFlow>b__57_1"),
                AccessTools.Method(typeof(Quest), "<get_QuestCompletionDialogFlow>b__57_3"),
                AccessTools.Method(typeof(Quest), "<get_QuestCompletionDialogFlow>b__57_5"),
            };

            Assert.All(branchMethods, method =>
            {
                Assert.NotNull(method);
                Assert.Contains(
                    Harmony.GetPatchInfo(method).Prefixes,
                    patch => patch.PatchMethod.DeclaringType?.Name.Contains("RewardBranchPatch") == true);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void GiverDeathOrderingIsSharedByTheIssueManagerPatch()
    {
        var harmony = new Harmony($"{nameof(GiverDeathOrderingIsSharedByTheIssueManagerPatch)}.{Guid.NewGuid():N}");
        try
        {
            harmony.PatchCategory(typeof(GameInterface).Assembly, GameInterface.HARMONY_QUESTS_CATEGORY);

            var method = AccessTools.Method(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroKilled));
            Assert.Contains(
                Harmony.GetPatchInfo(method).Prefixes,
                patch => patch.PatchMethod.DeclaringType?.Name == "HeroKilledPatch");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void ClientLifecycleDoesNotInstallASecondDailyTickPatch()
    {
        var targetMethods = typeof(IssueManagerClientLifecyclePatches)
            .GetMethod("TargetMethods", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, null) as IEnumerable<MethodBase>;

        Assert.DoesNotContain(targetMethods, method => method == AccessTools.Method(typeof(IssueManager), nameof(IssueManager.DailyTick)));
    }

    [Fact]
    public void ClientLifecycleGuardsSettlementCallbacks()
    {
        var harmony = new Harmony($"{nameof(ClientLifecycleGuardsSettlementCallbacks)}.{Guid.NewGuid():N}");
        try
        {
            harmony.PatchCategory(typeof(GameInterface).Assembly, GameInterface.HARMONY_QUESTS_CATEGORY);
            foreach (var method in new[]
            {
                AccessTools.Method(typeof(IssueManager), nameof(IssueManager.OnSettlementEntered)),
                AccessTools.Method(typeof(IssueManager), nameof(IssueManager.OnSettlementLeft)),
                AccessTools.Method(typeof(IssueManager), nameof(IssueManager.OnCharacterPortraitPopUpOpened)),
            })
            {
                Assert.NotNull(method);
                Assert.Contains(Harmony.GetPatchInfo(method).Prefixes,
                    patch => patch.PatchMethod.DeclaringType?.Name == nameof(IssueManagerClientLifecyclePatches));
            }
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }
}
