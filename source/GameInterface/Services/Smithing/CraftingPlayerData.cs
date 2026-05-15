using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.Smithing;

[ProtoContract]
public class CraftingPlayerData
{
    // Dictionary<HeroId, Dictionary<CraftingTemplateId, float>>
    [ProtoMember(1)]
    public Dictionary<string, Dictionary<string, float>> HeroOpenNewPartXpDictionary { get; }

    // Dictionary<HeroId, Dictionary<CraftingTemplateId, List<CraftingPieceId>>>
    [ProtoMember(2)]
    public Dictionary<string, Dictionary<string, List<string>>> HeroOpenedPartsDictionary { get; }

    // Dictionary<HeroId, List<ItemObjectId>
    [ProtoMember(3)]
    public Dictionary<string, List<string>> HeroCraftingItemsHistory { get; }

    public CraftingPlayerData(
        Dictionary<string, Dictionary<string, float>> heroOpenNewPartXpDictionary,
        Dictionary<string, Dictionary<string, List<string>>> heroOpenedPartsDictionary,
        Dictionary<string, List<string>> heroCraftingItemsHistory)
    {
        HeroOpenNewPartXpDictionary = heroOpenNewPartXpDictionary;
        HeroOpenedPartsDictionary = heroOpenedPartsDictionary;
        HeroCraftingItemsHistory = heroCraftingItemsHistory;
    }
}
