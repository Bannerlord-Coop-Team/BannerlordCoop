using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Messages.Collections;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
    IEnumerable<TroopRosterElement> UnpackTroopRosterData(TroopRosterData troopRosterData);

    /// <summary>
    /// Updates target roster with incoming data from the client.
    /// </summary>
    void UpdateWithData(TroopRoster targetTroopRoster, TroopRosterData packedTroopRosterElements, Hero mainHero);

    /// <summary>
    /// Runs troop recruitment logic for client requests.
    /// Returns how many cart entries were not recruited because the volunteers or the gold changed.
    /// </summary>
    int HandleOnRecruitmentDone(string mobilePartyId, TroopInfo[] troopsInCart);

    /// <summary>
    /// Players are able to change the order of their party roster.
    /// Used to pack the order of elements in a TroopRoster to reshuffle after apply deltas.
    /// </summary>
    TroopRosterOrderData PackTroopRosterOrderData(TroopRoster roster);
}

internal class TroopRosterInterface : ITroopRosterInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterInterface>();
    private readonly IObjectManager objectManager;
    private readonly IRecruitmentSideEffects recruitmentSideEffects;

    public TroopRosterInterface(
        IObjectManager objectManager,
        IRecruitmentSideEffects recruitmentSideEffects)
    {
        this.objectManager = objectManager;
        this.recruitmentSideEffects = recruitmentSideEffects;
    }

    public TroopRosterData PackTroopRosterData(TroopRoster troopRoster)
    {
        var elements = new List<TroopRosterElementData>();
        foreach (TroopRosterElement troopRosterElement in troopRoster.data)
        {
            if (troopRosterElement.Character == null)
                continue;

            if (!objectManager.TryGetHandleWithLogging(troopRosterElement.Character, out var characterId))
                continue;

            elements.Add(new TroopRosterElementData(characterId, troopRosterElement.Number, troopRosterElement.WoundedNumber, troopRosterElement.Xp));
        }

        return new TroopRosterData(elements);
    }

    public IEnumerable<TroopRosterElement> UnpackTroopRosterData(TroopRosterData troopRosterData)
    {
        if (troopRosterData.Data == null)
            yield break;

        foreach (var elementData in troopRosterData.Data)
        {
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(elementData.CharacterId, out var character))
                continue;

            yield return new TroopRosterElement(character)
            {
                _number = elementData.Number,
                _woundedNumber = elementData.WoundedNumber,
                _xp = elementData.Xp
            };
        }
    }

    public void UpdateWithData(TroopRoster targetTroopRoster, TroopRosterData packedTroopRosterElements, Hero mainHero)
    {
        // Only preserve heroes in a player's troopRoster
        bool preserveHeroes = mainHero != null && mainHero.IsPlayerHero() && targetTroopRoster.OwnerParty?.MemberRoster == targetTroopRoster;

        // If preserving heroes, clear without removing mainHero and heroes in the same clan (companions & family members)
        // Causes issues if mainHero, player companions or family members are removed from a player's party
        for (int i = targetTroopRoster._count - 1; i >= 0; i--)
        {
            var character = targetTroopRoster.data[i].Character;
            if (preserveHeroes && (character?.HeroObject == mainHero || character?.HeroObject?.Clan == mainHero.Clan)) continue;
            targetTroopRoster.AddToCounts(character, -targetTroopRoster.data[i].Number, false, -targetTroopRoster.data[i].WoundedNumber, 0, true);
        }

        if (packedTroopRosterElements.Data == null) return;

        // Rebuild roster with new data
        foreach (var element in UnpackTroopRosterData(packedTroopRosterElements))
        {
            // If preserving heroes, clear doesn't remove mainHero and companions
            // Avoid adding duplicates of any existing heroes to the roster when rebuilding
            if (preserveHeroes && targetTroopRoster.Contains(element.Character))
                continue;

            targetTroopRoster.Add(element);
        }
    }

    public int HandleOnRecruitmentDone(string mobilePartyId, TroopInfo[] troopsInCart)
    {
        troopsInCart ??= Array.Empty<TroopInfo>();

        if (!objectManager.TryGetObjectWithLogging(mobilePartyId, out MobileParty mobileParty)) return 0;

        List<(Hero, CharacterObject, int)> herosValidated = new();
        HashSet<(Hero, int)> claimedSlots = new();

        // Validate troops before committing to recruiting. The cart can be stale when an AI lord
        // or the daily volunteer update changed a slot after the client opened the recruit screen.
        foreach (var troop in troopsInCart)
        {
            if (!objectManager.TryGetObjectWithLogging(troop.RecruiterHeroId, out Hero hero)) continue;
            if (!objectManager.TryGetObjectWithLogging(troop.CharacterObjectId, out CharacterObject characterObject)) continue;

            var volunteerTypes = hero.VolunteerTypes;
            if (volunteerTypes == null || troop.TroopIndex < 0 || troop.TroopIndex >= volunteerTypes.Length) continue;

            var volunteerTroopAtIndex = volunteerTypes[troop.TroopIndex];

            // An emptied slot is null, so this also skips a troop another party recruited first
            if (volunteerTroopAtIndex != characterObject) continue;

            // Slots are only emptied at commit, so the same slot twice in one cart would recruit two troops
            if (!claimedSlots.Add((hero, troop.TroopIndex))) continue;

            herosValidated.Add((hero, volunteerTroopAtIndex, troop.TroopIndex));
        }

        int skipped = troopsInCart.Length - herosValidated.Count;
        if (skipped > 0)
        {
            Logger.Warning("Skipped {Skipped} of {Total} recruitment cart entries that did not match a free volunteer slot", skipped, troopsInCart.Length);
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
            return troopsInCart.Length;
        }

        // Commit recruitment
        foreach ((Hero hero, CharacterObject characterObject, int index) in herosValidated)
        {
            hero.VolunteerTypes[index] = null;
            MessageBroker.Instance.Publish(this, new VolunteerTypesArrayUpdated(hero, null, index));

            mobileParty.MemberRoster.AddToCounts(characterObject, 1, false, 0, 0, true, -1);
            recruitmentSideEffects.Apply(mobileParty, characterObject, 1);
        }

        GiveGoldAction.ApplyBetweenCharacters(mobileParty.LeaderHero, null, cost, false);

        return skipped;
    }

    public TroopRosterOrderData PackTroopRosterOrderData(TroopRoster roster)
    {
        var troopRosterOrderData = new TroopRosterOrderData(new());
        if (roster == null || roster.data == null) return null;

        for (int i = 0; i < roster.Count; i++)
        {
            var character = roster.data[i].Character;

            if (!objectManager.TryGetIdWithLogging(character, out var characterId)) continue;

            troopRosterOrderData.IndexCharacterIds[i] = characterId;
        }
        return troopRosterOrderData;
    }
}
