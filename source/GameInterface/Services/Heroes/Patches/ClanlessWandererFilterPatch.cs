using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(DefaultEncyclopediaHeroPage))]
internal class ClanlessWandererFilterPatch
{
    [HarmonyPatch(nameof(DefaultEncyclopediaHeroPage.InitializeFilterItems))]
    [HarmonyPostfix]
    public static void InitializeFilterItemsPostfix(ref IEnumerable<EncyclopediaFilterGroup> __result)
    {
        foreach (var group in __result)
        {
            if (group.Name == new TextObject("{=GZxFIeiJ}Occupation", null))
            {
                var filterItem = new EncyclopediaFilterItem(GameTexts.FindText("str_coop_clanless_wanderers"), h => ((Hero)h).Clan == null);
                group.Filters.Add(filterItem);
            }
        }
    }
}
