using HarmonyLib;
using TaleWorlds.CampaignSystem.TournamentGames;

namespace GameInterface.Services.Tournaments.Patches;

[HarmonyPatch(typeof(TournamentCampaignBehavior))]
internal class DisableTournamentCampaignBehavior
{
    [HarmonyPatch(nameof(TournamentCampaignBehavior.RegisterEvents))]
    static bool Prefix() => false;
}
