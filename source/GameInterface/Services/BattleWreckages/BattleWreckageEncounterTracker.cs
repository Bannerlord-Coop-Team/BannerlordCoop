using GameInterface.Services.BattleWreckages.Data;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.BattleWreckages;

/// <summary>
/// Server side wreckage encounters keyed by player hero, shared by the wreckage and campaign behavior interfaces
/// </summary>
public interface IBattleWreckageEncounterTracker : IGameAbstraction
{
    IReadOnlyList<BattleWreckageEncounter> Encounters { get; }

    void AddEncounter(BattleWreckageEncounter encounter);

    bool TryGetEncounter(Hero hero, out BattleWreckageEncounter encounter);

    bool RemoveEncounter(Hero hero);
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
}
