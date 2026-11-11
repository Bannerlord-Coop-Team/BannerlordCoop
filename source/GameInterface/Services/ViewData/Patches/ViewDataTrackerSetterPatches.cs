using Common;
using Common.Messaging;
using GameInterface.Services.ViewData.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.ViewData.Patches;

/// <summary>
/// Send the player's view data to the server when a saved field changes, so it's kept in the CoopSession for rejoining
/// </summary>
[HarmonyPatch]
internal class ViewDataTrackerSetterPatches
{
    private static readonly string[] Setters =
    {
        nameof(ViewDataTrackerCampaignBehavior.SetMapBarExtendedState),
        nameof(ViewDataTrackerCampaignBehavior.SetPartyTroopLocks),
        nameof(ViewDataTrackerCampaignBehavior.SetPartyPrisonerLocks),
        nameof(ViewDataTrackerCampaignBehavior.SetPartySortType),
        nameof(ViewDataTrackerCampaignBehavior.SetIsPartySortAscending),
        nameof(ViewDataTrackerCampaignBehavior.SetQuestSelection),
        nameof(ViewDataTrackerCampaignBehavior.SetQuestSortTypeSelection),
        nameof(ViewDataTrackerCampaignBehavior.AddEncyclopediaBookmarkToItem),
        nameof(ViewDataTrackerCampaignBehavior.RemoveEncyclopediaBookmarkFromItem),
        nameof(ViewDataTrackerCampaignBehavior.ClearCharacterNotification),
        nameof(ViewDataTrackerCampaignBehavior.OnArmyExamined),
        nameof(ViewDataTrackerCampaignBehavior.OnFigureheadUnlocked),
        nameof(ViewDataTrackerCampaignBehavior.OnFigureheadExamined),
    };

    // Includes every encyclopedia bookmark overload
    private static IEnumerable<MethodBase> TargetMethods() => AccessTools
        .GetDeclaredMethods(typeof(ViewDataTrackerCampaignBehavior))
        .Where(method => Setters.Contains(method.Name));

    [HarmonyPostfix]
    private static void Postfix(ViewDataTrackerCampaignBehavior __instance)
    {
        if (ModInformation.IsClient)
            MessageBroker.Instance.Publish(__instance, new ViewDataChanged(Hero.MainHero));
    }
}
