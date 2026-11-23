using System.Collections.Generic;

namespace GameInterface.Services.Ferries;

/// <summary>
/// [Client] Every player's ferry departure village (hero id to village id), copied from the server's session data on join
/// and kept up to date as ferries start and end.
/// </summary>
public interface IPlayerFerryTracker : IGameAbstraction
{
    IReadOnlyDictionary<string, string> Departures { get; }

    /// <summary>Replaces the departures with the server's.</summary>
    void Reset(Dictionary<string, string> departures);

    /// <summary>Records a player boarding a ferry at the village, or getting off one when <paramref name="villageId"/> is null.</summary>
    void Set(string playerHeroId, string villageId);
}

/// <inheritdoc cref="IPlayerFerryTracker"/>
internal class PlayerFerryTracker : IPlayerFerryTracker
{
    private readonly Dictionary<string, string> departures = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, string> Departures => departures;

    public void Reset(Dictionary<string, string> serverDepartures)
    {
        departures.Clear();
        if (serverDepartures == null) return;

        foreach (var departure in serverDepartures)
        {
            departures[departure.Key] = departure.Value;
        }
    }

    public void Set(string playerHeroId, string villageId)
    {
        if (villageId == null)
        {
            departures.Remove(playerHeroId);
            return;
        }

        departures[playerHeroId] = villageId;
    }
}
