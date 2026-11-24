using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.BattleWreckages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkShowWreckageInvestigationResults : ICommand
{
    [ProtoMember(1)]
    public readonly TroopRosterData LootedTroops;

    [ProtoMember(2)]
    public readonly ItemRosterElement[] LootedItems;

    [ProtoMember(3)]
    public readonly List<TextObject> ConsequenceExplanations;

    [ProtoMember(4)]
    public readonly int LootedGoldAmount;

    // Null unless the roll revealed a hideout
    [ProtoMember(5)]
    public readonly string FoundHideoutId;

    public NetworkShowWreckageInvestigationResults(
        TroopRosterData lootedTroops,
        ItemRosterElement[] lootedItems,
        List<TextObject> consequenceExplanations,
        int lootedGoldAmount,
        string foundHideoutId)
    {
        LootedTroops = lootedTroops;
        LootedItems = lootedItems;
        ConsequenceExplanations = consequenceExplanations;
        LootedGoldAmount = lootedGoldAmount;
        FoundHideoutId = foundHideoutId;
    }
}
