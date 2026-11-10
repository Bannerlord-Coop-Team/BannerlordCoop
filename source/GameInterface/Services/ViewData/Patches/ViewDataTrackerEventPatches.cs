using Common;
using Common.Messaging;
using GameInterface.Services.ViewData.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.ViewData.Patches;

/// <summary>
/// Campaign events that only sometimes change a saved field, so the view data is only sent when it did
/// </summary>
[HarmonyPatch(typeof(ViewDataTrackerCampaignBehavior))]
internal class ViewDataTrackerEventPatches
{
    [HarmonyPatch(nameof(ViewDataTrackerCampaignBehavior.OnHeroLevelledUp))]
    [HarmonyPostfix]
    private static void OnHeroLevelledUpPostfix(ViewDataTrackerCampaignBehavior __instance, Hero hero)
    {
        if (hero == Hero.MainHero) PublishChanged(__instance);
    }

    [HarmonyPatch(nameof(ViewDataTrackerCampaignBehavior.OnNewArmyCreated))]
    [HarmonyPrefix]
    private static void OnNewArmyCreatedPrefix(ViewDataTrackerCampaignBehavior __instance, out int __state)
    {
        __state = __instance._unExaminedArmies.Count;
    }

    [HarmonyPatch(nameof(ViewDataTrackerCampaignBehavior.OnNewArmyCreated))]
    [HarmonyPostfix]
    private static void OnNewArmyCreatedPostfix(ViewDataTrackerCampaignBehavior __instance, int __state)
    {
        if (__instance._unExaminedArmies.Count != __state) PublishChanged(__instance);
    }

    [HarmonyPatch(nameof(ViewDataTrackerCampaignBehavior.OnArmyDispersed))]
    [HarmonyPrefix]
    private static void OnArmyDispersedPrefix(ViewDataTrackerCampaignBehavior __instance, out int __state)
    {
        __state = __instance._unExaminedArmies.Count;
    }

    [HarmonyPatch(nameof(ViewDataTrackerCampaignBehavior.OnArmyDispersed))]
    [HarmonyPostfix]
    private static void OnArmyDispersedPostfix(ViewDataTrackerCampaignBehavior __instance, int __state)
    {
        if (__instance._unExaminedArmies.Count != __state) PublishChanged(__instance);
    }

    // Runs on every map bar refresh and edits the old dictionary in place, so a copy is compared
    [HarmonyPatch(nameof(ViewDataTrackerCampaignBehavior.UpdatePrisonerRecruitValue))]
    [HarmonyPrefix]
    private static void UpdatePrisonerRecruitValuePrefix(ViewDataTrackerCampaignBehavior __instance, out Dictionary<CharacterObject, int> __state)
    {
        __state = new Dictionary<CharacterObject, int>(__instance._examinedPrisonerCharacterList);
    }

    [HarmonyPatch(nameof(ViewDataTrackerCampaignBehavior.UpdatePrisonerRecruitValue))]
    [HarmonyPostfix]
    private static void UpdatePrisonerRecruitValuePostfix(ViewDataTrackerCampaignBehavior __instance, Dictionary<CharacterObject, int> __state)
    {
        var current = __instance._examinedPrisonerCharacterList;
        bool unchanged = current.Count == __state.Count &&
            current.All(entry => __state.TryGetValue(entry.Key, out var count) && count == entry.Value);

        if (!unchanged) PublishChanged(__instance);
    }

    private static void PublishChanged(ViewDataTrackerCampaignBehavior tracker)
    {
        if (ModInformation.IsClient)
            MessageBroker.Instance.Publish(tracker, new ViewDataChanged(Hero.MainHero));
    }
}
