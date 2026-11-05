using GameInterface.Configuration;
using System;
using System.Collections.Generic;

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

    private readonly IModConfig modConfig;

    public PreferredBattleHosts(IModConfig modConfig)
    {
        this.modConfig = modConfig;
    }

    private IReadOnlyList<string> Configured =>
        (IReadOnlyList<string>)modConfig.Data?.ModOptions?.PreferredBattleHosts ?? Array.Empty<string>();

    public int RankOf(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return Unlisted;

        var configured = Configured;
        for (int i = 0; i < configured.Count; i++)
        {
            if (string.Equals(configured[i]?.Trim(), controllerId, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return Unlisted;
    }
}
