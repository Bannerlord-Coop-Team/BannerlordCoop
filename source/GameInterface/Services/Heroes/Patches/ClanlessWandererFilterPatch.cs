using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Encyclopedia.Pages;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(DefaultEncyclopediaHeroPage))]
internal class ClanlessWandererFilterPatch
{
    [HarmonyPatch(nameof(DefaultEncyclopediaHeroPage.InitializeFilterItems))]
    [HarmonyPostfix]
    public static void InitializeFilterItemsPostfix(ref IEnumerable<EncyclopediaFilterGroup> __result)
    {
        var clanStatusList = new List<EncyclopediaFilterItem>
        {
            new(GameTexts.FindText("str_coop_clanless"), h => ((Hero)h).Clan == null),
            new(GameTexts.FindText("str_coop_in_clan"), h => ((Hero)h).Clan != null)
        };

        __result = __result.AddItem(new EncyclopediaFilterGroup(clanStatusList, GameTexts.FindText("str_coop_clan_status")));
    }
}
