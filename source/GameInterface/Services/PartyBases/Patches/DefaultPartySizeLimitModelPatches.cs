using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases.Patches;

[HarmonyPatch(typeof(DefaultPartySizeLimitModel))]
internal class DefaultPartySizeLimitModelPatches
{
    /// <summary>
    /// Makes the server calculate caravan size limits properly for player caravans
    /// Without it, player caravans receive desertions from the server when they shouldn't
    /// </summary>
    [HarmonyPatch(nameof(DefaultPartySizeLimitModel.CalculateMobilePartyMemberSizeLimit))]
    [HarmonyPrefix]
    public static bool CalculateMobilePartyMemberSizeLimitPrefix(DefaultPartySizeLimitModel __instance, ref ExplainedNumber __result, MobileParty party, bool includeDescriptions = false)
    {
        // Override result if party is a player caravan
        if (party.IsCaravan && party.Party.Owner != null && party.Party.Owner.IsPlayerHero())
        {
            ExplainedNumber result = new ExplainedNumber(20f, includeDescriptions, __instance._baseSizeText);

            int partySizeLimit = party.CaravanPartyComponent.IsElite ? 30 : 10;
            if (party.CaravanPartyComponent.CanHaveNavalNavigationCapability)
            {
                partySizeLimit = (party.CaravanPartyComponent.IsElite ? 46 : 33);
            }
            result.Add((float)partySizeLimit, __instance._randomSizeBonusTemporary, null);

            if (party.IsCurrentlyAtSea)
            {
                foreach (Ship ship in party.Ships)
                {
                    result.AddFactor(ship.CrewCapacityBonusFactor, ship.Name);
                }
            }

            __result = result;
            return false;
        }

        // If not a player caravan, calculate using regular logic
        return true;
    }

    [HarmonyPatch(nameof(DefaultPartySizeLimitModel.GetInitialPartySizeRatioForMobileParty))]
    [HarmonyPrefix]
    public static bool GetInitialPartySizeRatioForMobilePartyPrefix(ref float __result, MobileParty party, PartyTemplateObject partyTemplate)
    {
        // Override result if player caravan
        if (party.IsCaravan && party.Owner != null && party.Owner.IsPlayerHero())
        {
            __result = 1f;
            return false;
        }

        return true;
    }
}
