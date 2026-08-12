using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Messages.Collections;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Logging;
using GameInterface.Services.TroopRosters.Messages;
using Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.TroopRosters.Interfaces;

public interface ITroopRosterInterface : IGameAbstraction
{
    /// <summary>
    /// Pack troop roster elements to allow for sending over the network.
    /// The string Id can either represent a Hero Id or a CharacterObject Id.
    /// </summary>
    TroopRosterData PackTroopRosterData(TroopRoster troopRoster);

    /// <summary>
    /// Packs a complete roster atomically. A result/reward roster must never be
    /// shortened merely because one character has not reached the network registry.
    /// </summary>
    bool TryPackTroopRosterData(
        TroopRoster troopRoster,
        out TroopRosterData data);

    /// <summary>
    /// Unpack troop roster data into usable TroopRosterElements.
    /// Optional mainHero parameter for avoiding retrieving a duplicate of a player hero already in a roster.
    /// </summary>
    IEnumerable<TroopRosterElement> UnpackTroopRosterData(TroopRosterData troopRosterData);

    /// <summary>
    /// Resolves an entire packed roster atomically. Authoritative battle results
    /// must never be shortened because one static character is unavailable.
    /// </summary>
    bool TryUnpackTroopRosterData(
        TroopRosterData troopRosterData,
        out TroopRosterElement[] elements);

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
    /// Packs a roster delta atomically. Returns false instead of silently omitting a changed
    /// character whose network identity is not registered yet.
    /// </summary>
    bool TryPackTroopRosterDelta(
        TroopRoster current,
        TroopRoster initial,
        out TroopRosterData data);

    /// <summary>
    /// Validates and applies a set of packed deltas (produced by <see cref="PackTroopRosterDelta"/>).
    /// Nothing is applied when any resulting roster element would be invalid. All count reductions are
    /// applied before any additions across every roster so transferred heroes retain their party linkage.
    /// </summary>
    bool TryApplyTroopRosterDeltas(
        IReadOnlyList<(TroopRoster roster, TroopRosterData delta)> deltas);

    /// <summary>
    /// Runs troop recruitment logic for client requests.
    /// </summary>
    bool TryHandleOnRecruitmentDone(
        string mobilePartyId,
        TroopInfo[] troopsInCart,
        out string rejectionReason);

    /// <summary>
    /// Players are able to change the order of their party roster.
    /// Used to pack the order of elements in a TroopRoster to reshuffle after apply deltas.
    /// </summary>
    TroopRosterOrderData PackTroopRosterOrderData(TroopRoster roster);

    bool TryPackTroopRosterOrderData(
        TroopRoster roster,
        out TroopRosterOrderData data);
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
            if (!objectManager.TryGetIdWithLogging(
                    troopRosterElement.Character,
                    out var characterId))
                continue;
            elements.Add(new TroopRosterElementData(
                characterId,
                troopRosterElement.Number,
                troopRosterElement.WoundedNumber,
                troopRosterElement.Xp));
        }
        return new TroopRosterData(elements);
    }

    public bool TryPackTroopRosterData(
        TroopRoster troopRoster,
        out TroopRosterData data)
    {
        var elements = new List<TroopRosterElementData>();
        foreach (TroopRosterElement troopRosterElement in troopRoster.data)
        {
            if (troopRosterElement.Character == null)
                continue;

            if (!StaticObjectRegistration.TryEnsure(
                    objectManager,
                    troopRosterElement.Character,
                    out var characterId))
            {
                data = new TroopRosterData(
                    Array.Empty<TroopRosterElementData>());
                return false;
            }

            elements.Add(new TroopRosterElementData(characterId, troopRosterElement.Number, troopRosterElement.WoundedNumber, troopRosterElement.Xp));
        }

        data = new TroopRosterData(elements);
        return true;
    }

    public IEnumerable<TroopRosterElement> UnpackTroopRosterData(TroopRosterData troopRosterData)
    {
        if (troopRosterData.Data == null)
            yield break;

        foreach (var elementData in troopRosterData.Data)
        {
            if (!StaticObjectRegistration.TryResolve(
                    objectManager,
                    elementData.CharacterId,
                    out CharacterObject character))
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

    public TroopRosterData PackTroopRosterDelta(TroopRoster current, TroopRoster initial)
    {
        TryPackTroopRosterDelta(current, initial, out TroopRosterData data);
        return data;
    }

    public bool TryUnpackTroopRosterData(
        TroopRosterData troopRosterData,
        out TroopRosterElement[] elements)
    {
        TroopRosterElementData[] packed = troopRosterData.Data?.ToArray() ??
            Array.Empty<TroopRosterElementData>();
        var resolved = new TroopRosterElement[packed.Length];
        for (int i = 0; i < packed.Length; i++)
        {
            TroopRosterElementData elementData = packed[i];
            if (string.IsNullOrEmpty(elementData.CharacterId) ||
                elementData.Number < 0 ||
                elementData.WoundedNumber < 0 ||
                elementData.WoundedNumber > elementData.Number ||
                elementData.Xp < 0 ||
                !StaticObjectRegistration.TryResolve(
                    objectManager,
                    elementData.CharacterId,
                    out CharacterObject character))
            {
                elements = Array.Empty<TroopRosterElement>();
                return false;
            }

            resolved[i] = new TroopRosterElement(character)
            {
                _number = elementData.Number,
                _woundedNumber = elementData.WoundedNumber,
                _xp = elementData.Xp
            };
        }

        elements = resolved;
        return true;
    }

    public bool TryPackTroopRosterDelta(
        TroopRoster current,
        TroopRoster initial,
        out TroopRosterData data)
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
            int currentXp = cur.xp;
            if (cur.number == 0)
            {
                currentXp = 0;
            }

            int initialXp = init.xp;
            if (init.number == 0)
            {
                initialXp = 0;
            }

            int xpDelta = currentXp - initialXp;
            if (numberDelta == 0 && woundedDelta == 0 && xpDelta == 0)
                continue;

            if (!StaticObjectRegistration.TryEnsure(
                    objectManager,
                    character,
                    out var characterId))
            {
                data = new TroopRosterData(Array.Empty<TroopRosterElementData>());
                return false;
            }

            elements.Add(new TroopRosterElementData(characterId, numberDelta, woundedDelta, xpDelta));
        }

        data = new TroopRosterData(elements);
        return true;
    }

    public bool TryApplyTroopRosterDeltas(
        IReadOnlyList<(TroopRoster roster, TroopRosterData delta)> deltas)
    {
        if (deltas == null) return false;

        var elements = new List<(
            TroopRoster roster,
            CharacterObject character,
            TroopRosterElementData delta)>();
        var uniqueElements = new HashSet<(TroopRoster roster, CharacterObject character)>();
        foreach (var (roster, delta) in deltas)
        {
            if (roster == null) return false;

            if (delta.Data == null) continue;
            var currentByCharacter = SumByCharacter(roster);

            foreach (var elementData in delta.Data)
            {
                if (!objectManager.TryGetObjectWithLogging<CharacterObject>(elementData.CharacterId, out var character))
                    return false;
                if (!uniqueElements.Add((roster, character))) return false;

                currentByCharacter.TryGetValue(character, out var current);
                long finalNumber = current.number + elementData.Number;
                long finalWounded = current.wounded + elementData.WoundedNumber;
                long finalXp = current.xp + elementData.Xp;
                if (finalNumber < 0 ||
                    finalNumber > int.MaxValue ||
                    finalWounded < 0 ||
                    finalWounded > finalNumber ||
                    finalXp < 0 ||
                    finalXp > int.MaxValue ||
                    (elementData.Xp != 0 && finalNumber == 0 && finalXp != 0))
                {
                    Logger.Warning(
                        "Rejected troop roster delta for {CharacterId}: current=({CurrentNumber},{CurrentWounded},{CurrentXp}) delta=({NumberDelta},{WoundedDelta},{XpDelta})",
                        elementData.CharacterId,
                        current.number,
                        current.wounded,
                        current.xp,
                        elementData.Number,
                        elementData.WoundedNumber,
                        elementData.Xp);
                    return false;
                }

                elements.Add((roster, character, elementData));
            }
        }

        var snapshots = deltas
            .Select(entry => entry.roster)
            .Distinct()
            .ToDictionary(roster => roster, SumByCharacter);
        try
        {
            ApplyDeltaElements(elements, applyAdditions: false);
            ApplyDeltaElements(elements, applyAdditions: true);
            return true;
        }
        catch (System.Exception exception)
        {
            Logger.Error(
                exception,
                "Troop roster delta failed; restoring authoritative snapshots");
            RestoreRosterSnapshots(snapshots);
            return false;
        }
    }

    private void ApplyDeltaElements(
        IReadOnlyList<(TroopRoster roster, CharacterObject character, TroopRosterElementData delta)> elements,
        bool applyAdditions)
    {
        foreach (var element in elements)
        {
            bool isAddition = element.delta.Number >= 0;
            if (isAddition != applyAdditions) continue;

            troopRosterLogger.Debug(
                element.roster,
                "APPLY-DELTA pass={Pass} character={CharacterId} numberDelta={Number} woundedDelta={Wounded} xpDelta={Xp}",
                applyAdditions ? "add" : "remove",
                element.delta.CharacterId,
                element.delta.Number,
                element.delta.WoundedNumber,
                element.delta.Xp);

            element.roster.AddToCounts(
                element.character,
                element.delta.Number,
                false,
                element.delta.WoundedNumber,
                element.delta.Xp,
                true);
        }
    }

    private static bool RestoreRosterSnapshots(
        IReadOnlyDictionary<TroopRoster, Dictionary<CharacterObject,
            (int number, int wounded, int xp)>> snapshots)
    {
        bool restored = true;
        // Remove excess units from every roster before adding missing units back.
        // This preserves hero party linkage across a failed transfer.
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (var snapshot in snapshots)
            {
                var current = SumByCharacter(snapshot.Key);
                var characters = new HashSet<CharacterObject>(current.Keys);
                characters.UnionWith(snapshot.Value.Keys);
                foreach (CharacterObject character in characters)
                {
                    current.TryGetValue(character, out var actual);
                    snapshot.Value.TryGetValue(character, out var target);
                    int numberDelta = target.number - actual.number;
                    if ((numberDelta < 0) != (pass == 0))
                        continue;
                    if (!RestoreRosterElement(
                            snapshot.Key, character, target))
                        restored = false;
                }
            }
        }
        return restored;
    }

    private static bool RestoreRosterElement(
        TroopRoster roster,
        CharacterObject character,
        (int number, int wounded, int xp) target)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var current = SumByCharacter(roster);
            current.TryGetValue(character, out var actual);
            int numberDelta = target.number - actual.number;
            int woundedDelta = target.wounded - actual.wounded;
            int xpDelta = target.xp - actual.xp;
            if (numberDelta == 0 && woundedDelta == 0 && xpDelta == 0)
                return true;
            try
            {
                roster.AddToCounts(
                    character,
                    numberDelta,
                    false,
                    woundedDelta,
                    xpDelta,
                    true);
            }
            catch (System.Exception rollbackException)
            {
                Logger.Error(
                    rollbackException,
                    "Troop roster snapshot restore threw for {Character}",
                    character?.StringId);
            }
        }

        var final = SumByCharacter(roster);
        final.TryGetValue(character, out var finalValue);
        bool matches = finalValue == target;
        if (!matches)
            Logger.Error(
                "Troop roster snapshot restore did not converge for {Character}",
                character?.StringId);
        return matches;
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

    public bool TryHandleOnRecruitmentDone(
        string mobilePartyId,
        TroopInfo[] troopsInCart,
        out string rejectionReason)
    {
        rejectionReason = "Recruitment could not be completed.";
        if (!objectManager.TryGetObjectWithLogging(
                mobilePartyId, out MobileParty mobileParty) ||
            mobileParty?.LeaderHero == null ||
            troopsInCart == null || troopsInCart.Length == 0)
            return false;

        List<(Hero hero, CharacterObject character, int resolvedIndex, bool rebound)>
            herosValidated = new();
        HashSet<(Hero, int)> submittedSlots = new();
        HashSet<(Hero, int)> claimedSlots = new();
        Settlement currentSettlement = mobileParty.CurrentSettlement;
        if (currentSettlement == null)
        {
            rejectionReason = "You are no longer in that settlement.";
            return false;
        }

        // Validate troops before committing to recruiting
        foreach (var troop in troopsInCart)
        {
            if (!objectManager.TryGetObjectWithLogging(
                    troop.RecruiterHeroId, out Hero hero) ||
                !objectManager.TryGetObjectWithLogging(
                    troop.CharacterObjectId, out CharacterObject characterObject) ||
                troop.TroopIndex < 0 ||
                troop.TroopIndex >= hero.VolunteerTypes.Length ||
                !submittedSlots.Add((hero, troop.TroopIndex)) ||
                hero.CurrentSettlement != currentSettlement ||
                !currentSettlement.Notables.Contains(hero))
            {
                rejectionReason = "The requested volunteer slot is no longer valid.";
                return false;
            }

            int resolvedIndex = ResolveVolunteerSlot(
                mobileParty.LeaderHero,
                hero,
                characterObject,
                troop.TroopIndex,
                claimedSlots);
            if (resolvedIndex < 0)
            {
                rejectionReason =
                    "That volunteer changed while the recruitment screen was open. No troops were recruited; please try again.";
                return false;
            }

            claimedSlots.Add((hero, resolvedIndex));
            herosValidated.Add((
                hero,
                characterObject,
                resolvedIndex,
                resolvedIndex != troop.TroopIndex));
        }

        if ((long)mobileParty.Party.NumberOfAllMembers +
                herosValidated.Count > mobileParty.Party.PartySizeLimit)
        {
            rejectionReason = "Your party no longer has room for those recruits.";
            return false;
        }

        // Calculate cost before changing any data
        var cost = 0;
        foreach (var validated in herosValidated)
        {
            cost += Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(
                validated.character, mobileParty.LeaderHero).RoundedResultNumber;
        }

        // Do not apply recruitment if the player does not have enough gold
        if (cost > mobileParty.LeaderHero.Gold)
        {
            Logger.Warning("Attempted to recruit troops that cost more than the player had");
            rejectionReason = "You no longer have enough denars for this recruitment.";
            return false;
        }

        // Commit recruitment with compensation. A native event/patch exception
        // must not consume a volunteer, troop or denars while the transaction is
        // reported as rejected.
        int goldBefore = mobileParty.LeaderHero.Gold;
        var rosterBefore = new Dictionary<TroopRoster, Dictionary<
            CharacterObject, (int number, int wounded, int xp)>>
        {
            [mobileParty.MemberRoster] =
                SumByCharacter(mobileParty.MemberRoster)
        };
        var clearedSlots = new List<(Hero hero, CharacterObject troop, int index)>();
        try
        {
            GiveGoldAction.ApplyBetweenCharacters(
                mobileParty.LeaderHero, null, cost, false);
            foreach (var validated in herosValidated)
            {
                validated.hero.VolunteerTypes[validated.resolvedIndex] = null;
                clearedSlots.Add((
                    validated.hero,
                    validated.character,
                    validated.resolvedIndex));
                mobileParty.MemberRoster.AddToCounts(
                    validated.character, 1, false, 0, 0, true, -1);
            }
        }
        catch (System.Exception exception)
        {
            Logger.Error(
                exception,
                "Recruitment failed during commit; restoring authoritative state");
            RestoreRosterSnapshots(rosterBefore);
            foreach (var cleared in clearedSlots)
                cleared.hero.VolunteerTypes[cleared.index] = cleared.troop;
            mobileParty.LeaderHero.Gold = goldBefore;
            rejectionReason =
                "Recruitment could not be committed safely. Please try again.";
            return false;
        }

        // State is committed. Notifications and progression may fail, but must
        // never turn the accepted recruitment into a retryable transaction.
        var reboundSnapshots = new Dictionary<Hero, CharacterObject[]>();
        foreach (var validated in herosValidated)
        {
            try
            {
                if (validated.rebound)
                {
                    reboundSnapshots[validated.hero] =
                        validated.hero.VolunteerTypes.ToArray();
                }
                else
                {
                    MessageBroker.Instance.Publish(
                        this,
                        new VolunteerTypesArrayUpdated(
                            validated.hero,
                            null,
                            validated.resolvedIndex));
                }
            }
            catch (System.Exception exception)
            {
                Logger.Error(
                    exception,
                    "Recruitment committed, but volunteer broadcast failed");
            }
            try
            {
                CampaignEventDispatcher.Instance.OnUnitRecruited(
                    validated.character, 1);
            }
            catch (System.Exception exception)
            {
                Logger.Error(
                    exception,
                    "Recruitment committed, but progression event failed");
            }
        }

        if (reboundSnapshots.Count > 0)
        {
            try
            {
                MessageBroker.Instance.Publish(
                    this,
                    new VolunteersUpdated(reboundSnapshots));
            }
            catch (System.Exception exception)
            {
                Logger.Error(
                    exception,
                    "Recruitment committed, but rebound volunteer snapshot failed");
            }
        }

        rejectionReason = string.Empty;
        return true;
    }

    private static int ResolveVolunteerSlot(
        Hero recruitingHero,
        Hero notable,
        CharacterObject requestedCharacter,
        int requestedIndex,
        HashSet<(Hero, int)> claimedSlots)
    {
        if (SlotMatches(
                recruitingHero,
                notable,
                requestedCharacter,
                requestedIndex,
                claimedSlots))
            return requestedIndex;

        for (int index = 0; index < notable.VolunteerTypes.Length; index++)
        {
            if (index != requestedIndex && SlotMatches(
                    recruitingHero,
                    notable,
                    requestedCharacter,
                    index,
                    claimedSlots))
                return index;
        }

        return -1;
    }

    private static bool SlotMatches(
        Hero recruitingHero,
        Hero notable,
        CharacterObject requestedCharacter,
        int index,
        HashSet<(Hero, int)> claimedSlots)
    {
        return index >= 0 &&
            index < notable.VolunteerTypes.Length &&
            !claimedSlots.Contains((notable, index)) &&
            ReferenceEquals(notable.VolunteerTypes[index], requestedCharacter) &&
            HeroHelper.HeroCanRecruitFromHero(recruitingHero, notable, index);
    }

    public TroopRosterOrderData PackTroopRosterOrderData(TroopRoster roster)
    {
        TryPackTroopRosterOrderData(roster, out TroopRosterOrderData data);
        return data;
    }

    public bool TryPackTroopRosterOrderData(
        TroopRoster roster,
        out TroopRosterOrderData troopRosterOrderData)
    {
        troopRosterOrderData = new TroopRosterOrderData(new());
        if (roster == null || roster.data == null) return false;

        for (int i = 0; i < roster.Count; i++)
        {
            var character = roster.data[i].Character;

            if (!StaticObjectRegistration.TryEnsure(
                    objectManager,
                    character,
                    out var characterId))
            {
                troopRosterOrderData = null;
                return false;
            }

            troopRosterOrderData.IndexCharacterIds[i] = characterId;
        }
        return true;
    }
}
