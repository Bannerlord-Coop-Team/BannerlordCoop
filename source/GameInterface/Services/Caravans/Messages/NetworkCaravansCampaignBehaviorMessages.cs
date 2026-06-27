using Common.Messaging;
using GameInterface.Services.Caravans.Data;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Caravans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCaravansKingdomDestroyed : ICommand
{
    [ProtoMember(1)]
    public readonly string DestroyedKingdomId;

    public NetworkCaravansKingdomDestroyed(string destroyedKingdomId)
    {
        DestroyedKingdomId = destroyedKingdomId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCaravanPartyDestroyed : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    public NetworkCaravanPartyDestroyed(string mobilePartyId)
    {
        MobilePartyId = mobilePartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkDeleteExpiredTradeRumorTakenCaravans : ICommand
{
    [ProtoMember(1)]
    public readonly Dictionary<string, List<string>> PlayerExpiredCaravansRemovalLists;

    public NetworkDeleteExpiredTradeRumorTakenCaravans(
        Dictionary<string, List<string>> playerExpiredCaravansRemovalLists)
    {
        PlayerExpiredCaravansRemovalLists = playerExpiredCaravansRemovalLists;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkDeleteExpiredLootedCaravans : ICommand
{
    [ProtoMember(1)]
    public readonly List<string> DeletedLootedCaravansIdsList;

    public NetworkDeleteExpiredLootedCaravans(
        List<string> deletedLootedCaravansIdsList)
    {
        DeletedLootedCaravansIdsList = deletedLootedCaravansIdsList;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAddToLootedCaravans : ICommand
{
    [ProtoMember(1)]
    public readonly string CaravanPartyId;

    [ProtoMember(2)]
    public readonly CampaignTime CampaignTime;

    public NetworkAddToLootedCaravans(
        string caravanPartyId,
        CampaignTime campaignTime)
    {
        CaravanPartyId = caravanPartyId;
        CampaignTime = campaignTime;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateTradeActionLogsForParty : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly List<TradeActionLogData> TradeActionLogsData;

    public NetworkUpdateTradeActionLogsForParty(
        string mobilePartyId,
        List<TradeActionLogData> tradeActionLogsData)
    {
        MobilePartyId = mobilePartyId;
        TradeActionLogsData = tradeActionLogsData;
    }
}