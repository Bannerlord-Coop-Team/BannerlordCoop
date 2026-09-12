using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Messages;

public readonly struct UpdateCraftedItemHistory : IEvent
{
    public readonly Hero MainHero;
    public readonly List<ItemObject> CraftedItemHistory;

    public UpdateCraftedItemHistory(Hero mainHero, List<ItemObject> craftedItemHistory)
    {
        MainHero = mainHero;
        CraftedItemHistory = craftedItemHistory;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateCraftedItemHistory : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly List<string> CraftedItemHistoryIds;

    public NetworkUpdateCraftedItemHistory(string playerHeroId, List<string> craftedItemHistoryIds)
    {
        PlayerHeroId = playerHeroId;
        CraftedItemHistoryIds = craftedItemHistoryIds;
    }
}