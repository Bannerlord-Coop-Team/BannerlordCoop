using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Clans.Patches;

// Install before VM patches compile callers that can inline the original getter.
[HarmonyPatchCategory(GameInterface.HARMONY_STATIC_FIXES_CATEGORY)]
[HarmonyPatch(typeof(Clan), nameof(Clan.PlayerClan), MethodType.Getter)]
internal static class ClanPlayerClanPatch
{
    [HarmonyPrefix]
    public static bool Prefix(ref Clan __result)
    {
        if (Campaign.Current == null) return false;

        // Use the actual clan of the client's hero
        if (ModInformation.IsClient && (Game.Current?.PlayerTroop as CharacterObject)?.HeroObject?.Clan is Clan clan)
        {
            __result = clan;
            return false;
        }

        return true;
    }
}
