using Common.Logging;
using Common.Messaging;
using GameInterface.Services.CharacterDevelopers.Messages;
using GameInterface.Services.Clans;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper.PerkSelection;
using TaleWorlds.Library;

namespace GameInterface.Services.CharacterDevelopers.Patches;

[HarmonyPatch]
internal class ApplyChangesPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CharacterDeveloperVM>();

    [HarmonyPatch(typeof(CharacterDeveloperVM), nameof(CharacterDeveloperVM.GetApplicableHeroes))]
    [HarmonyPostfix]
    public static void GetApplicableHeroesPostfix(List<Hero> __result)
    {
        __result.RemoveAll(hero => !CoopClanPermissions.CanManageHero(hero));
    }

    [HarmonyPatch(typeof(CharacterDeveloperHeroItemVM), nameof(CharacterDeveloperHeroItemVM.ApplyChanges))]
    [HarmonyPrefix]
    public static bool ApplyChangesPrefix(ref CharacterDeveloperHeroItemVM __instance)
    {
        if (!CoopClanPermissions.CanManageHero(__instance.Hero)) return false;

        // Get data from CharacterDeveloperHeroItemVM
        HeroDeveloper heroDeveloper = __instance.HeroDeveloper;
        PerkSelectionVM perkSelection = __instance.PerkSelection;
        MBBindingList<CharacterAttributeItemVM> attributeSelection = __instance.Attributes;
        MBBindingList<SkillVM> skillSelection = __instance.Skills;

        // Publish message with data
        var message = new ApplyChanges(heroDeveloper, perkSelection, attributeSelection, skillSelection);
        MessageBroker.Instance.Publish(__instance, message);

        // Skip original to override original client saving
        return false;
    }
}
