using GameInterface.Services.Players.Data;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Smithing;

/// <summary>
/// A few dictionaries from CraftingCampaignBehavior assume there is only ever
/// one player. This data structure is used to save the same data for each player
/// mapped using their hero ids.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class CraftingPlayerData
{
    // Dictionary<PlayerHeroId, Dictionary<CraftingTemplateId, float>>
    [ProtoMember(1)]
    public Dictionary<string, Dictionary<string, float>> PlayerOpenNewPartXpDictionary { get; }

    // Dictionary<PlayerHeroId, Dictionary<CraftingTemplateId, List<CraftingPieceId>>>
    [ProtoMember(2)]
    public Dictionary<string, Dictionary<string, List<string>>> PlayerOpenedPartsDictionary { get; }

    // Dictionary<PlayerHeroId, List<ItemObjectId>
    [ProtoMember(3)]
    public Dictionary<string, List<string>> PlayerCraftedItemsHistory { get; }

    public CraftingPlayerData(
        Dictionary<string, Dictionary<string, float>> playerOpenNewPartXpDictionary,
        Dictionary<string, Dictionary<string, List<string>>> playerOpenedPartsDictionary,
        Dictionary<string, List<string>> playerCraftedItemsHistory)
    {
        PlayerOpenNewPartXpDictionary = playerOpenNewPartXpDictionary;
        PlayerOpenedPartsDictionary = playerOpenedPartsDictionary;
        PlayerCraftedItemsHistory = playerCraftedItemsHistory;
    }
}
