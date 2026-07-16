using Common;
using GameInterface.Extentions;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(RecruitPrisonersCampaignBehavior))]
internal class DisableRecruitPrisonersCampaignBehavior
{
    [HarmonyPatch(nameof(RecruitPrisonersCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(RecruitPrisonersCampaignBehavior))]
internal class RecruitPrisonersCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(RecruitPrisonersCampaignBehavior.HourlyTickMainParty))]
    [HarmonyPrefix]
    public static bool HourlyTickMainPartyPrefix(RecruitPrisonersCampaignBehavior __instance)
    {
        // Iterate over all player parties instead of just the server's party
        foreach (var playerParty in Campaign.Current.CampaignObjectManager.GetPlayerMobileParties())
        {
            TroopRoster memberRoster = playerParty.MemberRoster;
            TroopRoster prisonRoster = playerParty.PrisonRoster;
            if (memberRoster.Count != 0 && memberRoster.TotalManCount > 0 && prisonRoster.Count != 0 && prisonRoster.TotalRegulars > 0 && playerParty.MapEvent == null)
            {
                int numberOfPrisonersToCheck = MBRandom.RandomInt(0, prisonRoster.Count);
                bool troopConformed = false;
                for (int i = numberOfPrisonersToCheck; i < prisonRoster.Count + numberOfPrisonersToCheck; i++)
                {
                    int index = i % prisonRoster.Count;
                    CharacterObject characterAtIndex = prisonRoster.GetCharacterAtIndex(index);
                    if (characterAtIndex.IsRegular)
                    {
                        CharacterObject characterObject = characterAtIndex;
                        int elementNumber = playerParty.PrisonRoster.GetElementNumber(index);
                        int recruitableNumber = Campaign.Current.Models.PrisonerRecruitmentCalculationModel.CalculateRecruitableNumber(playerParty.Party, characterObject);
                        if (!troopConformed && recruitableNumber < elementNumber)
                        {
                            troopConformed = __instance.GenerateConformityForTroop(playerParty, characterObject, 1);
                        }
                    }

                    if (troopConformed)
                        break;
                }
            }
        }

        return false;
    }

    [HarmonyPatch(nameof(RecruitPrisonersCampaignBehavior.DailyTickAIMobileParty))]
    [HarmonyPrefix]
    public static bool DailyTickAIMobilePartyPrefix(MobileParty mobileParty)
    {
        // Block doing this daily tick on player parties
        return !mobileParty.IsPlayerParty();
    }
}