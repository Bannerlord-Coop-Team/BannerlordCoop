using Common;
using GameInterface.Extentions;
using GameInterface.Services.Clans.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(EmissarySystemCampaignBehavior))]
internal class EmissarySystemCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(EmissarySystemCampaignBehavior.DailyTick))]
    [HarmonyPrefix]
    public static bool DailyTickPrefix()
    {
        if (ModInformation.IsClient) return false;

        foreach (var playerClan in Campaign.Current.CampaignObjectManager.GetPlayerClans())
        {
            foreach (var hero in playerClan.Heroes)
            {
                if (!Campaign.Current.Models.EmissaryModel.IsEmissary(hero)) continue;

                float gainRelationChance = MBMath.ClampFloat(0.05f + 0.05f * (hero.GetSkillValue(DefaultSkills.Charm) / 300f), 0f, 1f);
                if (MBRandom.RandomFloat > gainRelationChance) continue;

                var suitableNotables = hero.CurrentSettlement.Notables;
                var suitableLords = hero.CurrentSettlement.HeroesWithoutParty.FindAll(n => !n.IsPrisoner && n.CharacterObject.Occupation == Occupation.Lord && !n.Clan.IsPlayerClan());

                bool hasNotables = suitableNotables.Count > 0;
                bool hasLords = suitableLords.Count > 0;

                if (hasNotables && hasLords)
                {
                    if (MBRandom.RandomFloat <= 0.5f) hasNotables = false;
                    else hasLords = false;
                }

                if (hasNotables) ApplyRelationChange(hero, suitableNotables);
                else if (hasLords) ApplyRelationChange(hero, suitableLords);
            }
        }

        return false;
    }

    private static void ApplyRelationChange(Hero emissary, List<Hero> candidates)
    {
        Hero randomHero = candidates.GetRandomElement();
        if (randomHero != null)
        {
            ChangeRelationAction.ApplyEmissaryRelation(emissary, randomHero, Campaign.Current.Models.EmissaryModel.EmissaryRelationBonusForMainClan);
        }
    }
}
