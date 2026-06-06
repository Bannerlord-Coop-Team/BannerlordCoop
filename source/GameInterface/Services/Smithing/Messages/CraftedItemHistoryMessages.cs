using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Messages;

public record CraftedItemHistoryUpdated : IEvent
{
    public Hero MainHero;
    public List<ItemObject> CraftedItemHistory;

    public CraftedItemHistoryUpdated(Hero mainHero, List<ItemObject> craftedItemHistory)
    {
        MainHero = mainHero;
        CraftedItemHistory = craftedItemHistory;
    }
}

[ProtoContract(SkipConstructor = true)]
public class NetworkUpdateCraftedItemHistory : ICommand
{
    [ProtoMember(1)]
    public string PlayerHeroId;

    [ProtoMember(2)]
    public List<string> CraftedItemHistoryIds;

    public NetworkUpdateCraftedItemHistory(string playerHeroId, List<string> craftedItemHistoryIds)
    {
        PlayerHeroId = playerHeroId;
        CraftedItemHistoryIds = craftedItemHistoryIds;
    }
}