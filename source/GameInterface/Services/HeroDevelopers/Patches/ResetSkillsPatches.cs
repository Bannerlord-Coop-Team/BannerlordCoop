using Common;
using Common.Messaging;
using GameInterface.Services.HeroDevelopers.Messages;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

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
    [HarmonyPrefix]
    public static bool ClearPerksForSkill(Hero hero, SkillObject skill)
    {
        if (ModInformation.IsClient) return false;

        foreach (PerkObject perkObject in PerkObject.All)
        {
            if (perkObject.Skill == skill)
            {
                PerkHelper.ClearPermanentBonusesIfExists(hero, perkObject);
                hero.SetPerkValueInternal(perkObject, false);
            }
        }
        hero.HitPoints = MathF.Min(hero.HitPoints, hero.MaxHitPoints);

        if (hero.PartyBelongedTo?.MemberRoster == null) return false;

        hero.PartyBelongedTo.MemberRoster.UpdateVersion();
       
        var message = new UpdateRosterVersionAfterPerkChange(hero.PartyBelongedTo.MemberRoster);
        MessageBroker.Instance.Publish(null, message);

        return false;
    }
}