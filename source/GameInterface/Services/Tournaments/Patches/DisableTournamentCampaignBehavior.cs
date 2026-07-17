using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.TournamentGames;

namespace GameInterface.Services.Tournaments.Patches;

[HarmonyPatch(typeof(TournamentCampaignBehavior))]
internal class DisableTournamentCampaignBehavior
{
    static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(TournamentCampaignBehavior), nameof(TournamentCampaignBehavior.DailyTickSettlement)),
        AccessTools.Method(typeof(TournamentCampaignBehavior), nameof(TournamentCampaignBehavior.OnSessionLaunched)),
        AccessTools.Method(typeof(TournamentCampaignBehavior), nameof(TournamentCampaignBehavior.OnNewGameCreatedPartialFollowUpEnd)),
        AccessTools.Method(typeof(TournamentCampaignBehavior), nameof(TournamentCampaignBehavior.OnHeroKilled)),
        AccessTools.Method(typeof(TournamentCampaignBehavior), nameof(TournamentCampaignBehavior.OnTournamentFinished)),
        AccessTools.Method(typeof(TournamentCampaignBehavior), nameof(TournamentCampaignBehavior.OnDailyTick)),
        AccessTools.Method(typeof(TournamentCampaignBehavior), nameof(TournamentCampaignBehavior.OnGameLoaded)),
        AccessTools.Method(typeof(TournamentCampaignBehavior), nameof(TournamentCampaignBehavior.OnTownRebelliousStateChanged)),
        AccessTools.Method(typeof(TournamentCampaignBehavior), nameof(TournamentCampaignBehavior.OnSiegeEventStarted)),
    };

    static bool Prefix() => ModInformation.IsServer;
}
