using Common;
using Common.Messaging;
using GameInterface.Services.Education.Interfaces;
using GameInterface.Services.Education.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Education.Patches;

[HarmonyPatch(typeof(EducationCampaignBehavior))]
internal class EducationCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(EducationCampaignBehavior.OnDailyTick))]
    [HarmonyPrefix]
    public static bool OnDailyTickPrefix()
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IEducationCampaignBehaviorInterface>(out var educationInterface)) return false;

        educationInterface.OnDailyTick();

        return false;
    }

    [HarmonyPatch(nameof(EducationCampaignBehavior.OnHeroComesOfAge))]
    [HarmonyPrefix]
    public static bool OnHeroComesOfAgePrefix(Hero hero)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IEducationCampaignBehaviorInterface>(out var educationInterface)) return false;

        educationInterface.OnHeroComesOfAge(hero);

        return false;
    }

    [HarmonyPatch(nameof(EducationCampaignBehavior.OnHeroKilled))]
    [HarmonyPrefix]
    public static bool OnHeroKilledPrefix(Hero victim)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IEducationCampaignBehaviorInterface>(out var educationInterface)) return false;

        educationInterface.OnHeroKilled(victim);

        return false;
    }

    [HarmonyPatch(nameof(EducationCampaignBehavior.OnFinalize))]
    [HarmonyPrefix]
    public static bool OnFinalizePrefix(EducationCampaignBehavior __instance, EducationCampaignBehavior.EducationStage stage, Hero child, List<string> chosenOptions)
    {
        if (ModInformation.IsClient)
        {
            // Closes the map notice
            CampaignEventDispatcher.Instance.OnChildEducationCompleted(child, EducationCampaignBehavior.ChildStateToAge(stage.Target));

            // Clean up previous educations on client
            __instance._previousEducations.Remove(child);

            short target = (short)stage.Target;
            MessageBroker.Instance.Publish(__instance, new EducationStageUpdated(child, target, chosenOptions));

            __instance._activeStage = null;
            __instance._activeChild = null;
        }
        else
        {
            if (!ContainerProvider.TryResolve<IEducationCampaignBehaviorInterface>(out var educationInterface)) return false;

            educationInterface.OnFinalize(child, (short)stage.Target, stage, chosenOptions);
        }

        return false;
    }
}