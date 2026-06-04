using Common.Logging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Interfaces;

public interface ITroopRosterInterface : IGameAbstraction
{
    /// <summary>
    /// Pack troop roster elements to allow for sending over the network.
    /// The string Id can either represent a Hero Id or a CharacterObject Id.
    /// </summary>
    TroopRosterData PackTroopRosterData(TroopRoster troopRoster);

    /// <summary>
    /// Updates target roster with incoming data from the client.
    /// </summary>
    void UpdateWithData(TroopRoster targetTroopRoster, TroopRosterData packedTroopRosterElements, Hero mainHero);
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

    public TroopRosterData PackTroopRosterData(TroopRoster troopRoster)
    {
        var packedData = new TroopRosterData(new());
        foreach (TroopRosterElement troopRosterElement in troopRoster.data)
        {
            if (!objectManager.TryGetIdWithLogging(troopRosterElement.Character?.HeroObject, out string characterId)
                && !objectManager.TryGetIdWithLogging(troopRosterElement.Character, out characterId)) continue;

            packedData.Data.Add(new TroopRosterElementData(characterId, troopRosterElement.Number, troopRosterElement.WoundedNumber, troopRosterElement.Xp));
        }

        return packedData;
    }

    public void UpdateWithData(TroopRoster targetTroopRoster, TroopRosterData packedTroopRosterElements, Hero mainHero)
    {
        if (packedTroopRosterElements.Data == null) return;

        // Clear without removing MainHero (causes issues if MainHero is removed)
        for (int i = targetTroopRoster._count - 1; i >= 0; i--)
        {
            if (targetTroopRoster.data[i].Character?.HeroObject == mainHero) continue;
            targetTroopRoster.AddToCounts(targetTroopRoster.data[i].Character, -targetTroopRoster.data[i].Number, false, -targetTroopRoster.data[i].WoundedNumber, 0, true);
        }

        // Rebuild roster with new data
        foreach (var troopRosterElementData in packedTroopRosterElements.Data)
        {
            TroopRosterElement troopRosterElement;
            if (objectManager.TryGetObjectWithLogging<Hero>(troopRosterElementData.CharacterId, out var hero))
            {
                if (hero == mainHero) continue;
                troopRosterElement = new TroopRosterElement(hero.CharacterObject);
            }
            else if (objectManager.TryGetObjectWithLogging<CharacterObject>(troopRosterElementData.CharacterId, out var character))
            {
                troopRosterElement = new TroopRosterElement(character);
            }
            else
            {
                continue;
            }

            troopRosterElement._number = troopRosterElementData.Number;
            troopRosterElement._woundedNumber = troopRosterElementData.WoundedNumber;
            troopRosterElement._xp = troopRosterElementData.Xp;
            targetTroopRoster.Add(troopRosterElement);
        }
    }
}
