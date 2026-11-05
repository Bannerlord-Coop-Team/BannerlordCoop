using GameInterface.Configuration;
using System;

namespace Missions.Battles;

/// <summary>Ranks players by the server's "preferredBattleHosts" mod-config list for battle host election.</summary>
public interface IPreferredBattleHosts
{
    /// <summary>The player's position in the list (0 is the most preferred), or <see cref="PreferredBattleHosts.Unlisted"/>
    /// when not listed, so unlisted players sort after every listed one. An empty list ranks everyone unlisted,
    /// which keeps the first mission-ready election.</summary>
    int RankOf(string controllerId);
}

/// <inheritdoc cref="IPreferredBattleHosts"/>
public class PreferredBattleHosts : IPreferredBattleHosts
{
    /// <summary>The rank of a player who is not listed, higher than any listed rank.</summary>
    public const int Unlisted = int.MaxValue;

    public int RankOf(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return Unlisted;

        var configured = ModConfigProvider.ModOptions.PreferredBattleHosts;
        for (int i = 0; i < configured.Count; i++)
        {
            if (string.Equals(configured[i]?.Trim(), controllerId, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return Unlisted;
    }
}
