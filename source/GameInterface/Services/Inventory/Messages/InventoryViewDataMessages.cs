using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Inventory.Messages;

public readonly struct SaveItemLockStates : IEvent
{
    public readonly Hero MainHero;
    public readonly IEnumerable<string> ItemLockIds;

    public SaveItemLockStates(
        Hero mainHero,
        IEnumerable<string> itemLockIds)
    {
        MainHero = mainHero;
        ItemLockIds = itemLockIds;
    }
}

public readonly struct SaveItemSortStates : IEvent
{
    public readonly Hero MainHero;
    public readonly int UsageType;
    public readonly Tuple<int, int> SortPreference;

    public SaveItemSortStates(
        Hero mainHero,
        int usageType,
        Tuple<int, int> sortPreference)
    {
        MainHero = mainHero;
        UsageType = usageType;
        SortPreference = sortPreference;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSaveItemLockStates : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly IEnumerable<string> ItemLockIds;

    public NetworkSaveItemLockStates(
        string mainHeroId,
        IEnumerable<string> itemLockIds)
    {
        MainHeroId = mainHeroId;
        ItemLockIds = itemLockIds;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSaveItemSortStates : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly int UsageType;
    
    [ProtoMember(3)]
    public readonly Tuple<int, int> SortPreference;

    public NetworkSaveItemSortStates(
        string mainHeroId,
        int usageType,
        Tuple<int, int> sortPreference)
    {
        MainHeroId = mainHeroId;
        UsageType = usageType;
        SortPreference = sortPreference;
    }
}