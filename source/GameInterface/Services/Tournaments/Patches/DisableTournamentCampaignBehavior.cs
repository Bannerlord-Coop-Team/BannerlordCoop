using HarmonyLib;
using TaleWorlds.CampaignSystem.TournamentGames;
using GameInterface.Policies;

namespace GameInterface.Services.Tournaments.Patches;

[HarmonyPatch(typeof(TournamentCampaignBehavior))]
internal class DisableTournamentCampaignBehavior
{
    [HarmonyPatch(nameof(TournamentCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
