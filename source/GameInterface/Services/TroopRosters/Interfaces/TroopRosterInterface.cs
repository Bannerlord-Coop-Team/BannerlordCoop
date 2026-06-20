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
    /// </summary>
    List<TroopRosterElement> UnpackTroopRosterData(TroopRosterData troopRosterData);

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
            // troopRoster.data is the backing array and includes empty padding slots past the live count;
            // those have no Character, so skip them before resolving ids. Otherwise every padding slot
            // logs a "null object" error, multiplied by every roster the snapshot packs each frame.
            if (troopRosterElement.Character == null) continue;

            // A roster element is either a hero (synced by its Hero id) or a basic troop (synced by its
            // CharacterObject id). Resolve the id that matches, rather than probing the Hero id first: a
            // basic troop has no HeroObject, and probing it would log a failed lookup for every basic troop.
            Hero hero = troopRosterElement.Character.HeroObject;
            bool isHero = hero != null;

            string characterId;
            if (isHero)
            {
                if (!objectManager.TryGetIdWithLogging(hero, out characterId)) continue;
            }
            else if (!objectManager.TryGetIdWithLogging(troopRosterElement.Character, out characterId)) continue;

            packedData.Data.Add(new TroopRosterElementData(characterId, troopRosterElement.Number, troopRosterElement.WoundedNumber, troopRosterElement.Xp, isHero));
        }

        return packedData;
    }

    public List<TroopRosterElement> UnpackTroopRosterData(TroopRosterData troopRosterData)
    {
        if (troopRosterData.Data == null) return new();

        var unpackedData = new List<TroopRosterElement>();
        foreach (var troopRosterElementData in troopRosterData.Data)
        {
            TroopRosterElement troopRosterElement;
            if (troopRosterElementData.IsHero)
            {
                if (!objectManager.TryGetObjectWithLogging<Hero>(troopRosterElementData.CharacterId, out var hero)) continue;
                troopRosterElement = new TroopRosterElement(hero.CharacterObject);
            }
            else
            {
                if (!objectManager.TryGetObjectWithLogging<CharacterObject>(troopRosterElementData.CharacterId, out var character)) continue;
                troopRosterElement = new TroopRosterElement(character);
            }

            troopRosterElement._number = troopRosterElementData.Number;
            troopRosterElement._woundedNumber = troopRosterElementData.WoundedNumber;
            troopRosterElement._xp = troopRosterElementData.Xp;
            unpackedData.Add(troopRosterElement);
        }

        return unpackedData;
    }

    public void UpdateWithData(TroopRoster targetTroopRoster, TroopRosterData packedTroopRosterElements, Hero mainHero)
    {
        // Preserve the local main hero and the player's companions across a whole-roster replace, but only
        // when replacing a party's own MEMBER roster: removing the main hero breaks the player party, and clan
        // equipment/inventory work relies on companions persisting there. Any other roster, a prison roster or
        // an ownerless prisoner-management roster, must mirror the server exactly, otherwise a companion the
        // server freed, ransomed, or sold stays stuck as a prisoner on the client. Detecting the member roster
        // affirmatively (rather than "not the prison roster") also fails safe for an ownerless roster, where
        // OwnerParty is null. The mainHero != null guard also stops a null mainHero matching a basic troop's
        // null HeroObject and wrongly preserving it.
        bool preserveHeroes = mainHero != null && targetTroopRoster.OwnerParty?.MemberRoster == targetTroopRoster;

        for (int i = targetTroopRoster._count - 1; i >= 0; i--)
        {
            var character = targetTroopRoster.data[i].Character;
            if (preserveHeroes && (character?.HeroObject == mainHero || character?.HeroObject?.IsPlayerCompanion == true)) continue;
            targetTroopRoster.AddToCounts(character, -targetTroopRoster.data[i].Number, false, -targetTroopRoster.data[i].WoundedNumber, 0, true);
        }

        if (packedTroopRosterElements.Data == null) return;

        // Rebuild from the snapshot. When preserving, the clear above left only the heroes we kept, so a hero
        // already present is one of those; skip re-adding it (Add merges by character and would double the
        // count). A snapshot hero that was NOT preserved locally (a companion the server just moved into this
        // roster) is absent here, so it is added normally and not lost. When not preserving, the clear emptied
        // the roster, so nothing is skipped.
        foreach (var element in UnpackTroopRosterData(packedTroopRosterElements))
        {
            if (preserveHeroes && targetTroopRoster.Contains(element.Character)) continue;
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