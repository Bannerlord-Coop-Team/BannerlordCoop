using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Patches;

using Issue = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue;
using Quest = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;
using Result = ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestResult;

[HarmonyPatch(typeof(Quest), nameof(Quest.ApplyQuestResult))]
internal class ExtortionQuestRewardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Quest __instance, in Result result, out IDisposable __state)
    {
        __state = null;
        if (ModInformation.IsServer)
        {
            if (ExtortionQuestContext.IsActive) return true;
            return ContainerProvider.TryResolve<IExtortionQuestContext>(out var context) &&
                context.TryEnter(__instance.QuestGiver, out __state);
        }
        if (ContainerProvider.TryResolve<IExtortionQuestRewards>(out var rewards)) rewards.Request(__instance, in result);
        return false;
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(TraitLevelingHelper), nameof(TraitLevelingHelper.AddTraitXp))]
internal class ExtortionQuestTraitXpPatch
{
    [HarmonyPrefix]
    private static bool Prefix(TraitObject trait, int xpAmount)
    {
        if (ModInformation.IsClient || !ExtortionQuestContext.IsActive) return true;
        // Reuse the persisted per-hero store already used by the other supported issue.
        GangLeaderNeedsToOffloadStolenGoodsQuestType.ApplyOwnerTraitXp(Hero.MainHero, trait, xpAmount);
        if (GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.TryGet(Hero.MainHero, out var progress))
            MessageBroker.Instance.Publish(Hero.MainHero, new ExtortionTraitXpChanged(Hero.MainHero, trait,
                progress.GetPropertyValue(trait)));
        return false;
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class ExtortionAlternativeCompletionContextPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Issue) return true;
        return ModInformation.IsServer && AlternativeSolutionCompletionAuthorityGuard.IsActive &&
            ContainerProvider.TryResolve<IExtortionQuestContext>(out var context) &&
            context.TryEnter(__instance.IssueOwner, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}
