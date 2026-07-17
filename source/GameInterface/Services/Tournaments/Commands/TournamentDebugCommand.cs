using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Tournaments.Commands;

public class TournamentDebugCommand
{
    [CommandLineArgumentFunction("add_tournament_to_town", "coop.debug.tournaments")]
    public static string AddTournamentToTown(List<string> args)
    {
        if (ModInformation.IsClient)
            return "This function can only be used by the server";

        if (args.Count != 1)
            return "Usage: coop.debug.tournaments.add_tournament_to_town <town name or id>";

        if (Campaign.Current?.TournamentManager is not TournamentManager tournamentManager)
            return "No campaign is currently loaded";

        string townIdentifier = args[0];
        Town town = Campaign.Current.CampaignObjectManager.Settlements
            .Where(settlement => settlement.IsTown)
            .FirstOrDefault(settlement =>
                string.Equals(settlement.StringId, townIdentifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(settlement.Town?.StringId, townIdentifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(settlement.Name?.ToString(), townIdentifier, StringComparison.OrdinalIgnoreCase))
            ?.Town;
        if (town == null)
            return $"Town '{townIdentifier}' not found";

        if (tournamentManager.GetTournamentGame(town) != null)
            return $"{town.Name} already has an active tournament";

        bool tournamentAdded = false;
        GameThread.RunSafe(
            () =>
            {
                tournamentManager.AddTournament(new FightTournamentGame(town));
                tournamentAdded = true;
            },
            blocking: true,
            context: nameof(AddTournamentToTown));

        return tournamentAdded
            ? $"Added a tournament to {town.Name}"
            : $"Failed to add a tournament to {town.Name}; check the log for details";
    }
}
