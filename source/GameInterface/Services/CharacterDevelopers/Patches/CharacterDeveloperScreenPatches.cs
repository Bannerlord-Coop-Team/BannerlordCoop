using HarmonyLib;
using SandBox.GauntletUI;

namespace GameInterface.Services.CharacterDevelopers.Patches;

[HarmonyPatch(typeof(GauntletCharacterDeveloperScreen))]
internal class CharacterDeveloperScreenPatches
{
    // Vanilla never refreshes while the screen is open.
    [HarmonyPatch(nameof(GauntletCharacterDeveloperScreen.OnFrameTick))]
    [HarmonyPostfix]
    public static void OnFrameTickPostfix(GauntletCharacterDeveloperScreen __instance)
    {
        var heroItem = __instance._dataSource?.CurrentCharacter;
        if (heroItem == null || heroItem.PerkSelection.IsActive) return;

        bool changed = heroItem.CurrentTotalXp != heroItem.HeroDeveloper.TotalXp;
        foreach (var skill in heroItem.Skills)
        {
            if (skill.Level == heroItem.Hero.GetSkillValue(skill.Skill) &&
                skill.CurrentSkillXP == heroItem.HeroDeveloper.GetSkillXpProgress(skill.Skill)) continue;

            // InitializeValues also resets the focus to the applied value, so keep any unapplied focus edit
            bool hasFocusEdit = skill.IsThereAnyChanges();
            int focus = skill.CurrentFocusLevel;
            skill.InitializeValues();
            if (hasFocusEdit) skill.CurrentFocusLevel = focus;
            changed = true;
        }

        if (changed) heroItem.RefreshCharacterValues();
    }
}
