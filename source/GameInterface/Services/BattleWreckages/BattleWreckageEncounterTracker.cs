using GameInterface.Services.BattleWreckages.Data;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Library;

namespace GameInterface.Services.BattleWreckages;

/// <summary>
/// Server side wreckage encounters keyed by player hero
/// </summary>
public interface IBattleWreckageEncounterTracker : IGameAbstraction
{
    IReadOnlyList<BattleWreckageEncounter> Encounters { get; }

    void AddEncounter(BattleWreckageEncounter encounter);

    bool TryGetEncounter(Hero hero, out BattleWreckageEncounter encounter);

    bool RemoveEncounter(Hero hero);

    /// <summary>
    /// Pauses a running investigation like vanilla's PlayerInterrupted leave: keeps the time left, at least an hour,
    /// and records when it was interrupted so it can expire.
    /// </summary>
    void Interrupt(BattleWreckageEncounter encounter, CampaignTime now);

    /// <summary>
    /// Encounters whose last interrupt is older than vanilla's expiry window, resumed or not, like vanilla's hourly check.
    /// </summary>
    IReadOnlyList<BattleWreckageEncounter> GetExpiredEncounters(CampaignTime now);
}

public class BattleWreckageEncounterTracker : IBattleWreckageEncounterTracker
{
    private readonly Dictionary<Hero, BattleWreckageEncounter> encounters = new Dictionary<Hero, BattleWreckageEncounter>();

    // A copy, so callers can remove encounters while looping over it
    public IReadOnlyList<BattleWreckageEncounter> Encounters => encounters.Values.ToList();

    public void AddEncounter(BattleWreckageEncounter encounter)
    {
        encounters[encounter.Hero] = encounter;
    }

    public bool TryGetEncounter(Hero hero, out BattleWreckageEncounter encounter)
    {
        return encounters.TryGetValue(hero, out encounter);
    }

    public bool RemoveEncounter(Hero hero)
    {
        return encounters.Remove(hero);
    }

    public void Interrupt(BattleWreckageEncounter encounter, CampaignTime now)
    {
        var elapsed = now - encounter.InvestigationStartTime;
        var remainingHours = MathF.Max(1f, (float)(encounter.RequiredInvestigationDuration.ToHours - elapsed.ToHours));
        encounter.RequiredInvestigationDuration = CampaignTime.Hours(remainingHours);
        encounter.LastInteractionTime = now;
        encounter.IsInvestigating = false;
        encounter.InvestigationStartTime = CampaignTime.Zero;
    }

    public IReadOnlyList<BattleWreckageEncounter> GetExpiredEncounters(CampaignTime now)
    {
        // A zero LastInteractionTime means the encounter was never interrupted
        return encounters.Values
            .Where(encounter => encounter.LastInteractionTime != CampaignTime.Zero &&
                (now - encounter.LastInteractionTime).ToHours > BattleWreckageCampaignBehavior.InterruptedSnapshotExpiryAsHours)
            .ToList();
    }
}
