using Common;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(HeroHelper))]
internal class HeroHelperPatches
{
    /// <summary>
    /// Replace check to compare clans rather than just using IsPlayerCompanion
    /// </summary>
    [HarmonyPatch(nameof(HeroHelper.UnderPlayerCommand))]
    [HarmonyPrefix]
    public static bool UnderPlayerCommandPrefix(ref bool __result, Hero hero)
    {
        // Server doesn't have a hero and can't have another hero under command
        if (ModInformation.IsServer) return false;

        __result = hero != null && 
            ((hero.MapFaction != null && hero.MapFaction.Leader == Hero.MainHero) 
            || (hero.IsNotable && hero.HomeSettlement.OwnerClan == Clan.PlayerClan) 
            || hero.CompanionOf != null && hero.CompanionOf == Clan.PlayerClan);

        return false;
    }
}
