using Common;
using HarmonyLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, new[] { typeof(Action) })]
internal class ExtortionQuestJournalViewPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestsVM __instance)
    {
        if (ModInformation.IsServer || !ContainerProvider.TryResolve<IExtortionQuestJournal>(out var journal)) return;
        foreach (var item in __instance.ActiveQuestsList.Where(item => !journal.IsVisible(item)).ToArray())
            __instance.ActiveQuestsList.Remove(item);
        foreach (var item in __instance.OldQuestsList.Where(item => !journal.IsVisible(item)).ToArray())
            __instance.OldQuestsList.Remove(item);
        if (__instance.SelectedQuest == null)
        {
            var selected = __instance.ActiveQuestsList.FirstOrDefault() ?? __instance.OldQuestsList.FirstOrDefault();
            if (selected != null) __instance.SetSelectedItem(selected);
        }
        __instance.IsThereAnyQuest = __instance.ActiveQuestsList.Count > 0 || __instance.OldQuestsList.Count > 0;
        __instance.RefreshValues();
    }
}

[HarmonyPatch(typeof(QuestsVM), nameof(QuestsVM.SetSelectedItem))]
internal class ExtortionQuestJournalSelectionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestItemVM quest) => ModInformation.IsServer ||
        !ContainerProvider.TryResolve<IExtortionQuestJournal>(out var journal) || journal.IsVisible(quest);
}
