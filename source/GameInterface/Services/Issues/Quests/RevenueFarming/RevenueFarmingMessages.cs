using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

internal enum RevenueFarmingAction
{
    Collect,
    Leave,
    ResolveIncident,
    PayGiver,
    PaySteward,
    PayDemand,
    BetrayGiver,
    BetrayDemand,
    Refresh,
}

[ProtoContract]
internal sealed class RequestRevenueFarmingAction : ICommand
{
    [ProtoMember(1)] public string GiverId { get; set; }
    [ProtoMember(2)] public string QuestId { get; set; }
    [ProtoMember(3)] public int Revision { get; set; }
    [ProtoMember(4)] public RevenueFarmingAction Action { get; set; }
    [ProtoMember(5)] public string Incident { get; set; }
    [ProtoMember(6)] public int Option { get; set; }
}

[ProtoContract]
internal sealed class NetworkRevenueFarmingState : IServerToClientCommand
{
    [ProtoMember(1)] public string GiverId { get; set; }
    [ProtoMember(2)] public string QuestId { get; set; }
    [ProtoMember(3)] public int Revision { get; set; }
    [ProtoMember(4)] public int RequestedDenars { get; set; }
    [ProtoMember(5)] public bool Collecting { get; set; }
    [ProtoMember(6)] public bool AllCollected { get; set; }
    [ProtoMember(7)] public string Incident { get; set; }
    [ProtoMember(8)] public bool AwaitingPayment { get; set; }
    [ProtoMember(9)] public string CollectionSettlementId { get; set; }
    [ProtoMember(10)] public RevenueFarmingVillageState[] Villages { get; set; }
    [ProtoMember(11)] public string[] UsedIncidents { get; set; } = Array.Empty<string>();
    [ProtoMember(12)] public RevenueFarmingJournalState[] Journal { get; set; }
    [ProtoMember(13)] public int ProgressLogIndex { get; set; }
    [ProtoMember(14)] public string Menu { get; set; }
    [ProtoMember(15)] public bool FinishEncounter { get; set; }
    [ProtoMember(16)] public bool RefreshPresentation { get; set; }
    [ProtoMember(17)] public string MenuSettlementId { get; set; }
    [ProtoMember(18)] public bool IsFinalizing { get; set; }
}

[ProtoContract]
internal sealed class RevenueFarmingVillageState
{
    [ProtoMember(1)] public string SettlementId { get; set; }
    [ProtoMember(2)] public int CollectedAmount { get; set; }
    [ProtoMember(3)] public int HourlyGain { get; set; }
    [ProtoMember(4)] public bool Done { get; set; }
    [ProtoMember(5)] public bool EventOccurred { get; set; }
    [ProtoMember(6)] public bool Raided { get; set; }
    [ProtoMember(7)] public float AdditionalProgress { get; set; }
}

[ProtoContract]
internal sealed class RevenueFarmingJournalState
{
    [ProtoMember(1)] public long TimeTicks { get; set; }
    [ProtoMember(2)] public TextObject Text { get; set; }
    [ProtoMember(3)] public TextObject TaskName { get; set; }
    [ProtoMember(4)] public int Progress { get; set; }
    [ProtoMember(5)] public int Range { get; set; }
    [ProtoMember(6)] public int Type { get; set; }
}
