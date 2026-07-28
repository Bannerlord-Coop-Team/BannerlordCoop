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
    /// Validates and applies a set of packed deltas (produced by <see cref="PackTroopRosterDelta"/>).
    /// Nothing is applied when any resulting roster element would be invalid. All count reductions are
    /// applied before any additions across every roster so transferred heroes retain their party linkage.
    /// </summary>
    bool TryApplyTroopRosterDeltas(
        IReadOnlyList<(TroopRoster roster, TroopRosterData delta)> deltas,
        out string rejectionReason);

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

    public bool TryApplyTroopRosterDeltas(
        IReadOnlyList<(TroopRoster roster, TroopRosterData delta)> deltas,
        out string rejectionReason)
    {
        if (!TryPrepareDeltaElements(deltas, out var elements, out rejectionReason))
            return false;

        // AddToCounts(hero, -n) nulls the hero's party linkage, so additions must be the last operation.
        ApplyDeltaElements(elements, applyAdditions: false);
        ApplyDeltaElements(elements, applyAdditions: true);
        rejectionReason = null;
        return true;
    }

    private bool TryPrepareDeltaElements(
        IReadOnlyList<(TroopRoster roster, TroopRosterData delta)> deltas,
        out List<ResolvedRosterDelta> resolvedElements,
        out string rejectionReason)
    {
        resolvedElements = new List<ResolvedRosterDelta>();
        rejectionReason = null;

        if (deltas == null)
        {
            rejectionReason = "The party changes were empty. Reopen the party screen and try again.";
            return false;
        }

        foreach (var (roster, delta) in deltas)
        {
            if (roster == null)
            {
                rejectionReason = "The party changed before these edits were applied. Reopen the party screen and try again.";
                return false;
            }

            if (delta.Data == null) continue;

            foreach (var elementData in delta.Data)
            {
                if (!objectManager.TryGetObjectWithLogging<CharacterObject>(elementData.CharacterId, out var character))
                {
                    rejectionReason = "A troop changed before these edits were applied. Reopen the party screen and try again.";
                    return false;
                }

                var resolved = FindResolvedDelta(resolvedElements, roster, character);
                if (resolved == null)
                {
                    resolved = new ResolvedRosterDelta(roster, character, elementData.CharacterId);
                    resolvedElements.Add(resolved);
                }

                resolved.Number += elementData.Number;
                resolved.Wounded += elementData.WoundedNumber;
                resolved.Xp += elementData.Xp;
            }
        }

        for (int i = resolvedElements.Count - 1; i >= 0; i--)
        {
            if (resolvedElements[i].IsEmpty)
            {
                resolvedElements.RemoveAt(i);
                continue;
            }

            if (!TryValidateResolvedDelta(resolvedElements[i], out rejectionReason))
                return false;
        }

        return true;
    }

    private static ResolvedRosterDelta FindResolvedDelta(
        List<ResolvedRosterDelta> elements,
        TroopRoster roster,
        CharacterObject character)
    {
        foreach (var element in elements)
        {
            if (ReferenceEquals(element.Roster, roster) &&
                ReferenceEquals(element.Character, character))
            {
                return element;
            }
        }

        return null;
    }

    private bool TryValidateResolvedDelta(ResolvedRosterDelta element, out string rejectionReason)
    {
        var current = GetElementState(element.Roster, element.Character);
        long finalNumber = current.number + element.Number;
        long finalWounded = current.wounded + element.Wounded;
        long finalXp = current.xp + element.Xp;

        bool exceedsIntegerRange =
            element.Number < int.MinValue || element.Number > int.MaxValue ||
            element.Wounded < int.MinValue || element.Wounded > int.MaxValue ||
            element.Xp < int.MinValue || element.Xp > int.MaxValue ||
            finalNumber > int.MaxValue ||
            finalWounded > int.MaxValue ||
            finalXp > int.MaxValue;

        // Retained XP at zero count means the source changed after the client snapshot.
        bool invalidResult =
            exceedsIntegerRange ||
            finalNumber < 0 ||
            finalWounded < 0 ||
            finalWounded > finalNumber ||
            finalXp < 0 ||
            (finalNumber == 0 && finalXp != 0);

        if (!invalidResult)
        {
            rejectionReason = null;
            return true;
        }

        Logger.Warning(
            "Rejected troop roster delta for {CharacterId}: current=({CurrentNumber},{CurrentWounded},{CurrentXp}) delta=({NumberDelta},{WoundedDelta},{XpDelta})",
            element.CharacterId,
            current.number,
            current.wounded,
            current.xp,
            element.Number,
            element.Wounded,
            element.Xp);
        rejectionReason = "The party changed before these edits were applied. Reopen the party screen and try again.";
        return false;
    }

    private void ApplyDeltaElements(IReadOnlyList<ResolvedRosterDelta> elements, bool applyAdditions)
    {
        foreach (var element in elements)
        {
            bool isAddition = element.Number >= 0;
            if (isAddition != applyAdditions) continue;

            troopRosterLogger.Debug(
                element.Roster,
                "APPLY-DELTA pass={Pass} character={CharacterId} numberDelta={Number} woundedDelta={Wounded} xpDelta={Xp}",
                applyAdditions ? "add" : "remove",
                element.CharacterId,
                element.Number,
                element.Wounded,
                element.Xp);

            element.Roster.AddToCounts(
                element.Character,
                (int)element.Number,
                false,
                (int)element.Wounded,
                (int)element.Xp,
                true);
        }
    }

    private static (long number, long wounded, long xp) GetElementState(
        TroopRoster roster,
        CharacterObject character)
    {
        long number = 0;
        long wounded = 0;
        long xp = 0;

        foreach (TroopRosterElement element in roster.data)
        {
            if (!ReferenceEquals(element.Character, character)) continue;

            number += element.Number;
            wounded += element.WoundedNumber;
            xp += element.Xp;
        }

        return (number, wounded, xp);
    }

    private sealed class ResolvedRosterDelta
    {
        public TroopRoster Roster { get; }
        public CharacterObject Character { get; }
        public string CharacterId { get; }
        public long Number { get; set; }
        public long Wounded { get; set; }
        public long Xp { get; set; }
        public bool IsEmpty => Number == 0 && Wounded == 0 && Xp == 0;

        public ResolvedRosterDelta(
            TroopRoster roster,
            CharacterObject character,
            string characterId)
        {
            Roster = roster;
            Character = character;
            CharacterId = characterId;
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
