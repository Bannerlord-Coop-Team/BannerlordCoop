using Common;
using Common.Messaging;
using GameInterface.Services.Education.Interfaces;
using GameInterface.Services.Education.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ViewModelCollection.Education;
using TaleWorlds.Core;

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

            // Skills are sent alongside the keys because Year8 rolls a random skill each time the stage is built
            var skills = new List<SkillObject>();
            foreach (string optionKey in chosenOptions)
            {
                var option = stage.GetOption(optionKey);
                if (option == null) continue;

                skills.AddRange(option.Skills);
            }

            short target = (short)stage.Target;
            MessageBroker.Instance.Publish(__instance, new EducationStageUpdated(child, target, chosenOptions, skills));

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

    [HarmonyPatch(nameof(EducationCampaignBehavior.GetStage), new[] { typeof(Hero) })]
    [HarmonyPrefix]
    public static bool GetStagePrefix(Hero child, ref EducationCampaignBehavior.EducationStage __result)
    {
        if (ModInformation.IsServer) return true;

        if (!ContainerProvider.TryResolve<IEducationCampaignBehaviorInterface>(out var educationInterface)) return true;
        if (!educationInterface.TryGetLockedStage(child, out var lockedStage)) return true;

        __result = lockedStage;
        return false;
    }
}

[HarmonyPatch(typeof(EducationVM))]
internal class EducationVMPatches
{
    [HarmonyPatch(MethodType.Constructor, new[]
    {
        typeof(Hero),
        typeof(Action<bool>),
        typeof(Action<EducationCampaignBehavior.EducationCharacterProperties[]>),
        typeof(Action<List<BasicCharacterObject>, List<Equipment>>)
    })]
    [HarmonyPrefix]
    public static void ConstructorPrefix(Hero child)
    {
        if (ModInformation.IsServer) return;

        if (!ContainerProvider.TryResolve<IEducationCampaignBehaviorInterface>(out var educationInterface)) return;

        educationInterface.LockEducationStage(child);
    }

    [HarmonyPatch(nameof(EducationVM.OnFinalize))]
    [HarmonyPostfix]
    public static void OnFinalizePostfix()
    {
        if (ModInformation.IsServer) return;

        if (!ContainerProvider.TryResolve<IEducationCampaignBehaviorInterface>(out var educationInterface)) return;

        educationInterface.UnlockEducationStage();
    }
}