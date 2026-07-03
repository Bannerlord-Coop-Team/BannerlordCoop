using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>Side-to-team resolution against the current mission, shared by the puppet and reinforcement spawn paths.</summary>
public static class BattleTeams
{
    /// <summary>The current mission's main team for a battle side (the player's enemy team when the side is unknown).</summary>
    public static Team Resolve(BattleSideEnum side)
    {
        return side switch
        {
            BattleSideEnum.Attacker => Mission.Current.AttackerTeam,
            BattleSideEnum.Defender => Mission.Current.DefenderTeam,
            _ => Mission.Current.PlayerEnemyTeam
        };
    }
}
