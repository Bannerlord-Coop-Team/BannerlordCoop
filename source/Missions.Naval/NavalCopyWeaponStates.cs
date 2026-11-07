using Missions.Messages;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// [Game thread] The last state each copy's weapon took from its authority. A copy's own State never follows the authority's
/// reload, so a hull swap on a successor reads the carried ammo and reload from here.
/// </summary>
public interface INavalCopyWeaponStates
{
    /// <summary>Remembers the state a copy's weapon just took from its authority.</summary>
    void Remember(MissionObject weapon, BattleShipWeaponState state);

    /// <summary>Takes the state a weapon about to be replaced was following, or null for one this client simulated.</summary>
    BattleShipWeaponState Take(MissionObject weapon);

    /// <summary>Forgets the weapons this client simulates now, which read their own State again.</summary>
    void Forget(IEnumerable<MissionObject> weapons);

    /// <summary>Forgets every weapon, as the battle ends.</summary>
    void Clear();
}

/// <inheritdoc cref="INavalCopyWeaponStates"/>
public class NavalCopyWeaponStates : INavalCopyWeaponStates
{
    private readonly Dictionary<MissionObject, BattleShipWeaponState> states = new Dictionary<MissionObject, BattleShipWeaponState>();

    // The latest state replaces the one before it.
    public void Remember(MissionObject weapon, BattleShipWeaponState state) => states[weapon] = state;

    // Removes the entry, as the weapon it belongs to is about to be replaced.
    public BattleShipWeaponState Take(MissionObject weapon)
    {
        if (!states.TryGetValue(weapon, out var state)) return null;

        states.Remove(weapon);
        return state;
    }

    // Drops each weapon's entry; weapons without one are skipped.
    public void Forget(IEnumerable<MissionObject> weapons)
    {
        foreach (var weapon in weapons) states.Remove(weapon);
    }

    // Drops every entry.
    public void Clear() => states.Clear();
}
