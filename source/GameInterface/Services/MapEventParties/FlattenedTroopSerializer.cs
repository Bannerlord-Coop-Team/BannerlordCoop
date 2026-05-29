using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
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
            if (element.Troop == null)
                continue;

            if (!objectManager.TryGetIdWithLogging(element.Troop, out var characterObjectId))
                continue;

            troops.Add(new FlattenedTroop(
                characterObjectId,
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
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(troop.CharacterObjectId, out var characterObject))
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
}

[ProtoContract]
internal readonly struct FlattenedTroop
{
    [ProtoMember(1)]
    public readonly string CharacterObjectId;
    [ProtoMember(2)]
    public readonly int UniqueSeed;
    [ProtoMember(3)]
    public readonly RosterTroopState State;
    [ProtoMember(4)]
    public readonly int Xp;
    [ProtoMember(5)]
    public readonly int XpGained;

    public FlattenedTroop(
        string characterObjectId,
        int uniqueSeed,
        RosterTroopState state,
        int xp,
        int xpGained)
    {
        CharacterObjectId = characterObjectId;
        UniqueSeed = uniqueSeed;
        State = state;
        Xp = xp;
        XpGained = xpGained;
    }
}