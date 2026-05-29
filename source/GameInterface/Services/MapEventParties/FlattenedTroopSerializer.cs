using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEventParties;

internal static class FlattenedTroopSerializer
{
    public static FlattenedTroop[] Serialize(
        FlattenedTroopRoster roster,
        IObjectManager objectManager)
    {
        if (roster == null) throw new ArgumentNullException(nameof(roster));
        if (objectManager == null) throw new ArgumentNullException(nameof(objectManager));

        var troops = new List<FlattenedTroop>();

        foreach (var element in roster)
        {
            var troop = element.Troop;

            if (troop == null)
                continue;

            var objectToResolve = troop.IsHero
                ? (object)troop.HeroObject
                : troop;

            if (objectToResolve == null)
                continue;

            if (!objectManager.TryGetIdWithLogging(objectToResolve, out var objectId))
                continue;

            troops.Add(new FlattenedTroop(
                objectId,
                troop.IsHero,
                element.Descriptor.UniqueSeed,
                element.State,
                element.Xp,
                element.XpGained));
        }

        return troops.ToArray();
    }

    public static FlattenedTroopRoster Deserialize(IEnumerable<FlattenedTroop> troops, IObjectManager objectManager)
    {
        if (troops == null)
            return new FlattenedTroopRoster();

        var troopArray = troops as FlattenedTroop[] ?? troops.ToArray();
        var roster = new FlattenedTroopRoster(troopArray.Length);

        foreach (var troop in troopArray)
        {
            if (!TryResolveCharacterObject(objectManager, troop, out var characterObject))
                continue;

            var descriptor = new UniqueTroopDescriptor(troop.UniqueSeed);

            var element = new FlattenedTroopRosterElement(
                characterObject,
                troop.State,
                troop.Xp,
                descriptor,
                troop.XpGained);

            roster[descriptor] = element;
        }

        return roster;
    }

    private static bool TryResolveCharacterObject(IObjectManager objectManager, FlattenedTroop troop, out CharacterObject characterObject)
    {
        characterObject = null;

        if (troop.IsHero)
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(troop.ObjectId, out var hero))
                return false;

            characterObject = hero.CharacterObject;
            return characterObject != null;
        }

        return objectManager.TryGetObjectWithLogging(
            troop.ObjectId,
            out characterObject);
    }
}

[ProtoContract]
internal readonly struct FlattenedTroop
{
    [ProtoMember(1)]
    public readonly string ObjectId;
    [ProtoMember(2)]
    public readonly bool IsHero;
    [ProtoMember(3)]
    public readonly int UniqueSeed;
    [ProtoMember(4)]
    public readonly RosterTroopState State;
    [ProtoMember(5)]
    public readonly int Xp;
    [ProtoMember(6)]
    public readonly int XpGained;

    public FlattenedTroop(string objectId, bool isHero, int uniqueSeed, RosterTroopState state, int xp, int xpGained)
    {
        ObjectId = objectId;
        IsHero = isHero;
        UniqueSeed = uniqueSeed;
        State = state;
        Xp = xp;
        XpGained = xpGained;
    }
}