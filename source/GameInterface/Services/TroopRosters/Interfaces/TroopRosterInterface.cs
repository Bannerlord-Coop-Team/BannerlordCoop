using Common.Logging;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Interfaces;

public interface ITroopRosterInterface : IGameAbstraction
{
    /// <summary>
    /// Pack troop roster elements to allow for sending over the network.
    /// The string Id can either represent a Hero Id or a CharacterObject Id.
    /// </summary>
    List<(string, int, int, int)> PackTroopRosterData(TroopRoster troopRoster);

    /// <summary>
    /// Updates target roster with incoming data from the client.
    /// </summary>
    void UpdateWithData(TroopRoster targetTroopRoster, List<(string, int, int, int)> packedTroopRosterElements, Hero mainHero);
}

internal class TroopRosterInterface : ITroopRosterInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterInterface>();
    private readonly IObjectManager objectManager;

    public TroopRosterInterface(
        IObjectManager objectManager)
    {
        this.objectManager = objectManager;
    }

    public List<(string, int, int, int)> PackTroopRosterData(TroopRoster troopRoster)
    {
        var packedData = new List<(string, int, int, int)>();
        foreach (TroopRosterElement troopRosterElement in troopRoster.data)
        {
            if (!objectManager.TryGetIdWithLogging(troopRosterElement.Character?.HeroObject, out string characterId)
                && !objectManager.TryGetIdWithLogging(troopRosterElement.Character, out characterId)) continue;

            packedData.Add((characterId, troopRosterElement.Number, troopRosterElement.WoundedNumber, troopRosterElement.Xp));
        }
        return packedData;
    }

    public void UpdateWithData(TroopRoster targetTroopRoster, List<(string, int, int, int)> packedTroopRosterElements, Hero mainHero)
    {
        if (packedTroopRosterElements == null) return;

        // Clear without removing MainHero (causes issues if MainHero is removed)
        for (int i = targetTroopRoster._count - 1; i >= 0; i--)
        {
            if (targetTroopRoster.data[i].Character?.HeroObject == mainHero) continue;
            targetTroopRoster.AddToCounts(targetTroopRoster.data[i].Character, -targetTroopRoster.data[i].Number, false, -targetTroopRoster.data[i].WoundedNumber, 0, true);
        }

        // Rebuild rooster with new data
        foreach ((string characterId, int number, int woundedNumber, int xp) in packedTroopRosterElements)
        {
            TroopRosterElement troopRosterElement;
            if (objectManager.TryGetObjectWithLogging<Hero>(characterId, out var hero))
            {
                if (hero == mainHero) continue;
                troopRosterElement = new TroopRosterElement(hero.CharacterObject);
            }
            else if (objectManager.TryGetObjectWithLogging<CharacterObject>(characterId, out var character))
            {
                troopRosterElement = new TroopRosterElement(character);
            }
            else
            {
                continue;
            }

            troopRosterElement._number = number;
            troopRosterElement._woundedNumber = woundedNumber;
            troopRosterElement._xp = xp;
            targetTroopRoster.Add(troopRosterElement);
        }
    }
}
