using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Messages.Collections;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Logging;
using GameInterface.Services.TroopRosters.Messages;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
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
    /// Packs the per-character difference between <paramref name="current"/> and <paramref name="initial"/>
    /// (current minus initial). Only changed characters are included; an unchanged troop - including a hero -
    /// nets to zero and is omitted, so the change can be re-applied as a delta on the server with no special
    /// handling for heroes or companions.
    /// </summary>
    TroopRosterData PackTroopRosterDelta(TroopRoster current, TroopRoster initial);

    /// <summary>
    /// Applies a set of packed deltas (produced by <see cref="PackTroopRosterDelta"/>) to their rosters.
    /// All count reductions are applied before any additions across every roster, so that when a hero is
    /// moved between rosters the addition is the last AddToCounts on that hero - otherwise the trailing
    /// removal nulls the hero's PartyBelongedTo / PartyBelongedToAsPrisoner.
    /// </summary>
    void ApplyTroopRosterDeltas(IReadOnlyList<(TroopRoster roster, TroopRosterData delta)> deltas);

    /// <summary>
    /// Runs troop recruitment logic for client requests.
    /// </summary>
    void HandleOnRecruitmentDone(string mobilePartyId, TroopInfo[] troopsInCart);

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
    private readonly ITroopRosterLogger troopRosterLogger;

    public TroopRosterInterface(
        IObjectManager objectManager,
        ITroopRosterLogger troopRosterLogger)
    {
        this.objectManager = objectManager;
        this.troopRosterLogger = troopRosterLogger;
    }

    public TroopRosterData PackTroopRosterData(TroopRoster troopRoster)
    {
        var elements = new List<TroopRosterElementData>();
        foreach (TroopRosterElement troopRosterElement in troopRoster.data)
        {
            if (troopRosterElement.Character == null)
                continue;

            if (!objectManager.TryGetIdWithLogging(troopRosterElement.Character, out var characterId))
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

        // If preserving heroes, clear without removing mainHero and player companions
        // Causes issues if mainHero or player companions are removed from a player's party
        for (int i = targetTroopRoster._count - 1; i >= 0; i--)
        {
            var character = targetTroopRoster.data[i].Character;
            if (preserveHeroes && (character?.HeroObject == mainHero || character?.HeroObject?.IsPlayerCompanion == true)) continue;
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

    public TroopRosterData PackTroopRosterDelta(TroopRoster current, TroopRoster initial)
    {
        // Diffed via per-character totals (not raw slots), so any quirk present in both snapshots cancels.
        var currentCounts = SumByCharacter(current);
        var initialCounts = SumByCharacter(initial);

        var elements = new List<TroopRosterElementData>();
        foreach (var character in currentCounts.Keys.Union(initialCounts.Keys))
        {
            currentCounts.TryGetValue(character, out var cur);
            initialCounts.TryGetValue(character, out var init);

            int numberDelta = cur.number - init.number;
            int woundedDelta = cur.wounded - init.wounded;
            int xpDelta = cur.xp - init.xp;
            if (numberDelta == 0 && woundedDelta == 0 && xpDelta == 0)
                continue;

            if (!objectManager.TryGetIdWithLogging(character, out var characterId))
                continue;

            elements.Add(new TroopRosterElementData(characterId, numberDelta, woundedDelta, xpDelta));
        }

        return new TroopRosterData(elements);
    }

    public void ApplyTroopRosterDeltas(IReadOnlyList<(TroopRoster roster, TroopRosterData delta)> deltas)
    {
        // Two passes so that a hero moved from one roster to another is removed from the source before it is
        // added to the destination. AddToCounts(hero, -n) fires OnHeroRemoved which unconditionally nulls the
        // hero's party linkage, so the addition must be the last AddToCounts to win - regardless of the order
        // the rosters are listed in.
        ApplyDeltaElements(deltas, applyAdditions: false);
        ApplyDeltaElements(deltas, applyAdditions: true);
    }

    private void ApplyDeltaElements(IReadOnlyList<(TroopRoster roster, TroopRosterData delta)> deltas, bool applyAdditions)
    {
        foreach (var (roster, delta) in deltas)
        {
            if (delta.Data == null) continue;

            foreach (var elementData in delta.Data)
            {
                // Reductions (Number < 0) go in the removal pass; additions and pure wounded/xp changes
                // (Number >= 0) go in the addition pass. Each element is applied exactly once.
                bool isAddition = elementData.Number >= 0;
                if (isAddition != applyAdditions) continue;

                if (!objectManager.TryGetObjectWithLogging<CharacterObject>(elementData.CharacterId, out var character))
                    continue;

                troopRosterLogger.Debug(roster, "APPLY-DELTA pass={Pass} character={CharacterId} numberDelta={Number} woundedDelta={Wounded} xpDelta={Xp}",
                    applyAdditions ? "add" : "remove", elementData.CharacterId, elementData.Number, elementData.WoundedNumber, elementData.Xp);

                roster.AddToCounts(character, elementData.Number, false, elementData.WoundedNumber, elementData.Xp, true);
            }
        }
    }

    private static Dictionary<CharacterObject, (int number, int wounded, int xp)> SumByCharacter(TroopRoster roster)
    {
        var counts = new Dictionary<CharacterObject, (int number, int wounded, int xp)>();
        if (roster == null) return counts;

        foreach (TroopRosterElement element in roster.data)
        {
            if (element.Character == null) continue;
            counts.TryGetValue(element.Character, out var existing);
            counts[element.Character] = (existing.number + element.Number, existing.wounded + element.WoundedNumber, existing.xp + element.Xp);
        }
        return counts;
    }

    public void HandleOnRecruitmentDone(string mobilePartyId, TroopInfo[] troopsInCart)
    {
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

        GiveGoldAction.ApplyBetweenCharacters(mobileParty.LeaderHero, null, cost, false);
    }

    public TroopRosterOrderData PackTroopRosterOrderData(TroopRoster roster)
    {
        var troopRosterOrderData = new TroopRosterOrderData(new());
        for (int i = 0; i < roster.data.Length; i++)
        {
            var character = roster.data[i].Character;

            if (!objectManager.TryGetIdWithLogging(character, out var characterId)) continue;

            troopRosterOrderData.IndexCharacterIds[i] = characterId;
        }
        return troopRosterOrderData;
    }
}