using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(MobilePartyTrainingBehavior))]
internal class DisableMobilePartyTrainingBehavior
{
    [HarmonyPatch(nameof(MobilePartyTrainingBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

// OnPlayerUpgradedTroops already replaced in PartyDoneLogicHandler.ApplyUpgradedTroopHistory
[HarmonyPatch(typeof(MobilePartyTrainingBehavior))]
internal class MobilePartyTrainingBehaviorPatches
{
    [HarmonyPatch(nameof(MobilePartyTrainingBehavior.CheckScouting))]
    [HarmonyPrefix]
    public static bool CheckScoutingPrefix(MobileParty mobileParty)
    {
        if (mobileParty.EffectiveScout != null && !mobileParty.IsCurrentlyAtSea)
        {
            TerrainType faceTerrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mobileParty.CurrentNavigationFace);
            if (!mobileParty.IsPlayerParty()) // Replace MainParty check
            {
                SkillLevelingManager.OnAIPartiesTravel(mobileParty.EffectiveScout, mobileParty.IsCaravan, faceTerrainType);
            }
            SkillLevelingManager.OnTraverseTerrain(mobileParty, faceTerrainType);
        }

        return false;
    }

    [HarmonyPatch(nameof(MobilePartyTrainingBehavior.CheckMovementSkills))]
    [HarmonyPrefix]
    public static bool CheckMovementSkillsPrefix(MobileParty mobileParty)
    {
        // Only need to patch first block as mobileParty == MobileParty.MainParty won't be true on the server
        if (mobileParty.IsPlayerParty())
        {
            if (mobileParty.IsCurrentlyAtSea)
            {
                SkillLevelingManager.OnTravelOnWater(mobileParty, mobileParty._lastCalculatedSpeed);
                return false;
            }
            using (List<TroopRosterElement>.Enumerator enumerator = mobileParty.MemberRoster.GetTroopRoster().GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TroopRosterElement troopRosterElement = enumerator.Current;
                    if (troopRosterElement.Character.IsHero)
                    {
                        if (troopRosterElement.Character.Equipment.Horse.IsEmpty)
                        {
                            SkillLevelingManager.OnTravelOnFoot(troopRosterElement.Character.HeroObject, mobileParty._lastCalculatedSpeed);
                        }
                        else
                        {
                            SkillLevelingManager.OnTravelOnHorse(troopRosterElement.Character.HeroObject, mobileParty._lastCalculatedSpeed);
                        }
                    }
                }
                return false;
            }
        }

        return true;
    }
}