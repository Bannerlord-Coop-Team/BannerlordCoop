namespace GameInterface.Services.UI.PlayerNameplates;

/// <summary>Evaluates mission team relationships for player nameplates.</summary>
public interface IPlayerNameplateEligibility
{
    bool IsAlliedTeam(
        bool isPlayerTeam,
        bool isEnemyOfPlayerTeam,
        bool bothTeamsInvalid,
        bool hasMixedTeamValidity,
        bool isTournament);
}

/// <summary>Uses explicit mission hostility instead of the battle-side shortcut.</summary>
public sealed class PlayerNameplateEligibility : IPlayerNameplateEligibility
{
    public bool IsAlliedTeam(
        bool isPlayerTeam,
        bool isEnemyOfPlayerTeam,
        bool bothTeamsInvalid,
        bool hasMixedTeamValidity,
        bool isTournament)
    {
        if (bothTeamsInvalid) return true;
        if (hasMixedTeamValidity) return isTournament;

        return isPlayerTeam || !isEnemyOfPlayerTeam;
    }
}
