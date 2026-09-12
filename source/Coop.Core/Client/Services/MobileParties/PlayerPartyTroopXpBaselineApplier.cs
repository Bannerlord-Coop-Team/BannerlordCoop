using Common;
using Common.Logging;
using Common.Util;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace Coop.Core.Client.Services.MobileParties;

/// <summary>Restores a received player-party regular-troop XP baseline.</summary>
public interface IPlayerPartyTroopXpBaselineApplier
{
    bool TryApply(TroopRosterXpBaseline[] baselines);
}

internal sealed class PlayerPartyTroopXpBaselineApplier : IPlayerPartyTroopXpBaselineApplier
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerPartyTroopXpBaselineApplier>();

    private readonly IObjectManager objectManager;

    public PlayerPartyTroopXpBaselineApplier(IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public bool TryApply(TroopRosterXpBaseline[] baselines)
    {
        if (baselines == null) return true;

        var resolved = new List<(TroopRoster Roster, int Index, int Xp)>();
        foreach (var baseline in baselines)
        {
            if (!objectManager.TryGetObjectWithLogging<TroopRoster>(baseline.RosterId, out var roster))
                return false;

            foreach (var entry in baseline.Entries ?? Array.Empty<TroopXpBaselineEntry>())
            {
                if (!objectManager.TryGetObjectWithLogging<CharacterObject>(entry.CharacterId, out var character))
                    return false;

                int index = roster.FindIndexOfTroop(character);
                if (index < 0)
                {
                    Logger.Warning(
                        "Cannot apply troop XP baseline: {Character} is not in roster {Roster}",
                        entry.CharacterId,
                        baseline.RosterId);
                    return false;
                }
                resolved.Add((roster, index, entry.Xp));
            }
        }

        using (new AllowedThread())
        {
            foreach (var item in resolved)
            {
                item.Roster.SetElementXp(item.Index, item.Xp);
            }
        }

        return true;
    }
}
