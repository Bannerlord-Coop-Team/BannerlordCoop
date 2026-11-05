using GameInterface.Configuration;
using System;
using System.Collections.Generic;

namespace Missions.Battles;

/// <summary>Ranks players by the server's "preferredBattleHosts" mod-config list for battle host election.</summary>
public interface IPreferredBattleHosts
{
    /// <summary>True when no preferred hosts are configured, which keeps the first mission-ready election.</summary>
    bool IsEmpty { get; }

    /// <summary>The player's position in the list (0 is the most preferred), or -1 when not listed.</summary>
    int RankOf(string controllerId);
}

/// <inheritdoc cref="IPreferredBattleHosts"/>
public class PreferredBattleHosts : IPreferredBattleHosts
{
    private readonly IModConfig modConfig;

    public PreferredBattleHosts(IModConfig modConfig)
    {
        this.modConfig = modConfig;
    }

    private IReadOnlyList<string> Configured =>
        (IReadOnlyList<string>)modConfig.Data?.ModOptions?.PreferredBattleHosts ?? Array.Empty<string>();

    public bool IsEmpty
    {
        get
        {
            foreach (var entry in Configured)
                if (!string.IsNullOrWhiteSpace(entry)) return false;
            return true;
        }
    }

    public int RankOf(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return -1;

        var configured = Configured;
        for (int i = 0; i < configured.Count; i++)
        {
            if (string.Equals(configured[i]?.Trim(), controllerId, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }
}
