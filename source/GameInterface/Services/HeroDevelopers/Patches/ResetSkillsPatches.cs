using Common;
using Common.Messaging;
using GameInterface.Services.HeroDevelopers.Messages;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.HeroDevelopers.Patches;

[HarmonyPatch(typeof(HeroDeveloper))]
internal class ResetSkillsPatches
{
    [HarmonyPatch(nameof(HeroDeveloper.ClearFocuses))]
    [HarmonyPostfix]
    public static void ClearFocusesPostfix(HeroDeveloper __instance)
    {
        if (ModInformation.IsClient) return;

        var message = new ClearFocuses(__instance);
        MessageBroker.Instance.Publish(__instance, message);
    }
}

[HarmonyPatch(typeof(PerkHelper))]
internal class PerkHelperPatches
{
    [HarmonyPatch(nameof(PerkHelper.ClearPerksForSkill))]
    [HarmonyPostfix]
    public static void ClearPerksForSkill(Hero hero)
    {
        if (ModInformation.IsClient) return;

        hero.PartyBelongedTo?.MemberRoster?.UpdateVersion();

        var message = new UpdateRosterVersionAfterPerkChange(hero.PartyBelongedTo.MemberRoster);
        MessageBroker.Instance.Publish(null, message);
    }
}