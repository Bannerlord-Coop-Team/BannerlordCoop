using HarmonyLib;
using TaleWorlds.CampaignSystem.TournamentGames;
using Common;

namespace GameInterface.Services.Tournaments.Patches;

[HarmonyPatch(typeof(TournamentCampaignBehavior))]
internal class DisableTournamentCampaignBehavior
{
    [HarmonyPatch(nameof(TournamentCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}
