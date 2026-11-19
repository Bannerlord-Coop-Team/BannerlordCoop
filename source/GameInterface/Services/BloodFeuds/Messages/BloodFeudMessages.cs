using Common.Messaging;
using GameInterface.Services.BloodFeuds.Data;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.BloodFeuds.Messages;

public readonly struct InitializeClientBloodFeudData : IEvent
{
    public readonly BloodFeudPlayerData BloodFeudPlayerData;

    public InitializeClientBloodFeudData(BloodFeudPlayerData bloodFeudPlayerData)
    {
        BloodFeudPlayerData = bloodFeudPlayerData;
    }
}

/// <summary>
/// Every blood feud of the receiving client's player, keyed by clan id
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateBloodFeuds : ICommand
{
    [ProtoMember(1)]
    public readonly Dictionary<string, BloodFeudData> Feuds;

    public NetworkUpdateBloodFeuds(Dictionary<string, BloodFeudData> feuds)
    {
        Feuds = feuds;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyBloodFeudStateChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;
    [ProtoMember(2)]
    public readonly string ExecutedHeroId;
    [ProtoMember(3)]
    public readonly ChangeBloodFeudStateAction.ChangeBloodFeudActionDetail Detail;
    [ProtoMember(4)]
    public readonly int PenalizedClanCount;

    public NetworkNotifyBloodFeudStateChanged(string clanId, string executedHeroId, ChangeBloodFeudStateAction.ChangeBloodFeudActionDetail detail, int penalizedClanCount)
    {
        ClanId = clanId;
        ExecutedHeroId = executedHeroId;
        Detail = detail;
        PenalizedClanCount = penalizedClanCount;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyBloodFeudClanMemberExecuted : ICommand
{
    [ProtoMember(1)]
    public readonly string ExecutedHeroId;
    [ProtoMember(2)]
    public readonly string ExecutorHeroId;

    public NetworkNotifyBloodFeudClanMemberExecuted(string executedHeroId, string executorHeroId)
    {
        ExecutedHeroId = executedHeroId;
        ExecutorHeroId = executorHeroId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyBloodFeudLordExecuted : ICommand
{
    [ProtoMember(1)]
    public readonly string ExecutedHeroId;
    [ProtoMember(2)]
    public readonly string ClanMemberId;

    public NetworkNotifyBloodFeudLordExecuted(string executedHeroId, string clanMemberId)
    {
        ExecutedHeroId = executedHeroId;
        ClanMemberId = clanMemberId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyBloodFeudClanMemberCaptured : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanMemberId;
    [ProtoMember(2)]
    public readonly string FeudedClanId;

    public NetworkNotifyBloodFeudClanMemberCaptured(string clanMemberId, string feudedClanId)
    {
        ClanMemberId = clanMemberId;
        FeudedClanId = feudedClanId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyBloodFeudSettlementExecutionStarted : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanMemberId;
    [ProtoMember(2)]
    public readonly string FeudedClanId;
    [ProtoMember(3)]
    public readonly string SettlementId;
    [ProtoMember(4)]
    public readonly long ExecutionDateTicks;

    public NetworkNotifyBloodFeudSettlementExecutionStarted(string clanMemberId, string feudedClanId, string settlementId, long executionDateTicks)
    {
        ClanMemberId = clanMemberId;
        FeudedClanId = feudedClanId;
        SettlementId = settlementId;
        ExecutionDateTicks = executionDateTicks;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyBloodFeudSettlementExecutionCancelled : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanMemberId;
    [ProtoMember(2)]
    public readonly string FeudedClanId;

    public NetworkNotifyBloodFeudSettlementExecutionCancelled(string clanMemberId, string feudedClanId)
    {
        ClanMemberId = clanMemberId;
        FeudedClanId = feudedClanId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSettleBloodFeudByRansomPayment : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    public NetworkSettleBloodFeudByRansomPayment(string clanId)
    {
        ClanId = clanId;
    }
}
