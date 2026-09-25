using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// Non-leader player parties shouldn't count toward the limit of parties in a clan
/// </summary>
[HarmonyPatch(typeof(DefaultClanTierModel))]
internal class DefaultClanTierModelPatches
{
    [HarmonyPatch(nameof(DefaultClanTierModel.GetPartyLimitForTier))]
    [HarmonyPostfix]
    public static void GetPartyLimitForTierPostfix(ref int __result, Clan clan)
    {
        foreach (var warPartyComponent in clan.WarPartyComponents)
        {
            if (warPartyComponent.MobileParty.IsPlayerParty() && warPartyComponent.Party.LeaderHero != clan.Leader)
            {
                __result += 1;
            }
        }
    }
}
