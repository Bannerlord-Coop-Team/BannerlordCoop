using GameInterface.Services.Tournaments.Data;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace GameInterface.Services.Tournaments.Handlers;

internal sealed partial class TournamentSessionHandler
{
    private void PayWinningBetAndForfeitOthers(
        TournamentSessionSnapshot snapshot,
        CharacterObject winner)
    {
        TournamentContestantData winnerData = snapshot.Contestants
            .FirstOrDefault(contestant => contestant.SlotId == snapshot.WinnerSlotId);
        if (winnerData?.ControllerId != null)
        {
            string winnerKey = GetBetKey(snapshot.SessionId, winnerData.ControllerId);
            if (betLedger.TryGetValue(winnerKey, out var bet) &&
                playerManager.TryGetPlayer(winnerData.ControllerId, out var player) &&
                TryResolvePlayer(player, out var hero, out _))
            {
                GiveGoldAction.ApplyBetweenCharacters(null, hero, bet.ExpectedPayout, true);
            }
        }

        foreach (string controllerId in snapshot.Contestants
                     .Where(contestant => contestant.ControllerId != null)
                     .Select(contestant => contestant.ControllerId)
                     .Distinct())
        {
            string reason = controllerId == winnerData?.ControllerId
                ? "Tournament bet paid"
                : "Tournament bet forfeited";
            SettleBetLedger(snapshot.SessionId, controllerId, snapshot.Revision, null, reason);
        }
    }

    private static void ApplyTournamentProgression(Town town, MBList<CharacterObject> participants)
    {
        if (Campaign.Current?.Models?.TournamentModel == null || town == null)
            return;

        var progression = Campaign.Current.Models.TournamentModel.GetSkillXpGainFromTournament(town);
        foreach (CharacterObject participant in participants)
        {
            if (!participant.IsHero || participant.HeroObject?.HeroDeveloper == null)
                continue;
            participant.HeroObject.HeroDeveloper.AddSkillXp(
                progression.Item1,
                progression.Item2,
                true,
                true);
        }
    }
}
