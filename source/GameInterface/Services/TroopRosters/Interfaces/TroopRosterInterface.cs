using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Messages.Collections;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Messages;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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
    /// Unpack troop roster data into usable TroopRosterElements.
    /// Optional mainHero parameter for avoiding retrieving a duplicate of a player hero already in a roster.
    /// </summary>
    List<TroopRosterElement> UnpackTroopRosterData(TroopRosterData troopRosterData, Hero mainHero = null);

    /// <summary>
    /// Updates target roster with incoming data from the client.
    /// </summary>
    void UpdateWithData(TroopRoster targetTroopRoster, TroopRosterData packedTroopRosterElements, Hero mainHero);

    /// <summary>
    /// Runs troop recruitment logic for client requests.
    /// </summary>
    void HandleOnRecruitmentDone(string mobilePartyId, TroopInfo[] troopsInCart, out int changedGold);
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

    public List<TroopRosterElement> UnpackTroopRosterData(TroopRosterData troopRosterData, Hero mainHero = null)
    {
        if (troopRosterData.Data == null) return new();

        var unpackedData = new List<TroopRosterElement>();
        foreach (var elementData in troopRosterData.Data)
        {
            TroopRosterElement troopRosterElement;
            if (objectManager.TryGetObjectWithLogging<Hero>(elementData.CharacterId, out var hero))
            {
                if (hero == mainHero) continue;
                troopRosterElement = new TroopRosterElement(hero.CharacterObject);
            }
            else if (objectManager.TryGetObjectWithLogging<CharacterObject>(elementData.CharacterId, out var character))
            {
                troopRosterElement = new TroopRosterElement(character);
            }
            else
            {
                continue;
            }

            troopRosterElement._number = elementData.Number;
            troopRosterElement._woundedNumber = elementData.WoundedNumber;
            troopRosterElement._xp = elementData.Xp;
            unpackedData.Add(troopRosterElement);
        }

        return unpackedData;
    }

    public void UpdateWithData(TroopRoster targetTroopRoster, TroopRosterData packedTroopRosterElements, Hero mainHero)
    {
        // Clear without removing MainHero (causes issues if MainHero is removed)
        for (int i = targetTroopRoster._count - 1; i >= 0; i--)
        {
            if (targetTroopRoster.data[i].Character?.HeroObject == mainHero) continue;
            targetTroopRoster.AddToCounts(targetTroopRoster.data[i].Character, -targetTroopRoster.data[i].Number, false, -targetTroopRoster.data[i].WoundedNumber, 0, true);
        }

        if (packedTroopRosterElements.Data == null) return;

        // Rebuild roster with new data
        foreach (var element in UnpackTroopRosterData(packedTroopRosterElements, mainHero))
        {
            targetTroopRoster.Add(element);
        }
    }

    public void HandleOnRecruitmentDone(string mobilePartyId, TroopInfo[] troopsInCart, out int changedGold)
    {
        changedGold = 0;

        if (!objectManager.TryGetObjectWithLogging(mobilePartyId, out MobileParty mobileParty)) return;

        List<(Hero, CharacterObject, int)> herosValidated = new();

        // Validate troops before committing to recruiting
        foreach (var troop in troopsInCart)
        {
            if (!objectManager.TryGetObjectWithLogging(troop.RecruiterHeroId, out Hero hero)) continue;
            if (!objectManager.TryGetObjectWithLogging(troop.CharacterObjectId, out CharacterObject characterObject)) continue;

            var volunteerTroopAtIndex = hero.VolunteerTypes[troop.TroopIndex];

            if (volunteerTroopAtIndex is null) continue;

            herosValidated.Add((hero, characterObject, troop.TroopIndex));
        }

        // Calculate cost before changing any data
        var cost = 0;
        foreach ((Hero hero, CharacterObject characterObject, int index) in herosValidated)
        {
            cost += Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(characterObject, mobileParty.LeaderHero).RoundedResultNumber;
        }

        // Do not apply recruitment if the player does not have enough gold
        if (cost > mobileParty.LeaderHero.Gold)
        {
            Logger.Warning("Attempted to recruit troops that cost more than the player had");
            return;
        }

        // Commit recruitment
        foreach ((Hero hero, CharacterObject characterObject, int index) in herosValidated)
        {
            hero.VolunteerTypes[index] = null;
            MessageBroker.Instance.Publish(this, new VolunteerTypesArrayUpdated(hero, null, index));

            mobileParty.MemberRoster.AddToCounts(characterObject, 1, false, 0, 0, true, -1);
            CampaignEventDispatcher.Instance.OnUnitRecruited(characterObject, 1);
        }

        mobileParty.LeaderHero.Gold -= cost;
        changedGold = -cost;
    }
}
