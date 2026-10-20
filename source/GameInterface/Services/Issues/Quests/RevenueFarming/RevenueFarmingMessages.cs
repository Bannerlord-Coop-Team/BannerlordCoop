using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

public enum RevenueFarmingAction
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
    Desert,
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestRevenueFarmingAction : ICommand
{
    [ProtoMember(1)] public string GiverId { get; init; }
    [ProtoMember(2)] public string QuestId { get; init; }
    [ProtoMember(3)] public int Revision { get; init; }
    [ProtoMember(4)] public RevenueFarmingAction Action { get; init; }
    [ProtoMember(5)] public string Incident { get; init; }
    [ProtoMember(6)] public int Option { get; init; }

    public RequestRevenueFarmingAction(string giverId, string questId, int revision, RevenueFarmingAction action,
        string incident = null, int option = 0)
    {
        GiverId = giverId;
        QuestId = questId;
        Revision = revision;
        Action = action;
        Incident = incident;
        Option = option;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRevenueFarmingState : IServerToClientCommand
{
    private readonly string[] _usedIncidents;

    [ProtoMember(1)] public string GiverId { get; init; }
    [ProtoMember(2)] public string QuestId { get; init; }
    [ProtoMember(3)] public int Revision { get; init; }
    [ProtoMember(4)] public int RequestedDenars { get; init; }
    [ProtoMember(5)] public bool Collecting { get; init; }
    [ProtoMember(6)] public bool AllCollected { get; init; }
    [ProtoMember(7)] public string Incident { get; init; }
    [ProtoMember(8)] public bool AwaitingPayment { get; init; }
    [ProtoMember(9)] public string CollectionSettlementId { get; init; }
    [ProtoMember(10)] public RevenueFarmingVillageState[] Villages { get; init; }
    [ProtoMember(11)] public string[] UsedIncidents
    {
        get => _usedIncidents ?? Array.Empty<string>();
        init => _usedIncidents = value;
    }
    [ProtoMember(12)] public RevenueFarmingJournalState[] Journal { get; init; }
    [ProtoMember(13)] public int ProgressLogIndex { get; init; }
    [ProtoMember(14)] public string Menu { get; init; }
    [ProtoMember(15)] public bool FinishEncounter { get; init; }
    [ProtoMember(16)] public bool RefreshPresentation { get; init; }
    [ProtoMember(17)] public string MenuSettlementId { get; init; }
    [ProtoMember(18)] public bool IsFinalizing { get; init; }

    public NetworkRevenueFarmingState(string giverId, string questId, int revision, int requestedDenars,
        bool collecting, bool allCollected, string incident, bool awaitingPayment, string collectionSettlementId,
        RevenueFarmingVillageState[] villages, string[] usedIncidents, RevenueFarmingJournalState[] journal,
        int progressLogIndex, string menu, bool finishEncounter, bool refreshPresentation, string menuSettlementId,
        bool isFinalizing)
    {
        GiverId = giverId;
        QuestId = questId;
        Revision = revision;
        RequestedDenars = requestedDenars;
        Collecting = collecting;
        AllCollected = allCollected;
        Incident = incident;
        AwaitingPayment = awaitingPayment;
        CollectionSettlementId = collectionSettlementId;
        Villages = villages;
        UsedIncidents = usedIncidents ?? Array.Empty<string>();
        Journal = journal;
        ProgressLogIndex = progressLogIndex;
        Menu = menu;
        FinishEncounter = finishEncounter;
        RefreshPresentation = refreshPresentation;
        MenuSettlementId = menuSettlementId;
        IsFinalizing = isFinalizing;
    }

    public NetworkRevenueFarmingState WithPresentation(int revision = -1, string menu = null, bool finishEncounter = false,
        bool refreshPresentation = false, string menuSettlementId = null, bool isFinalizing = false) => new NetworkRevenueFarmingState(
            GiverId, QuestId, revision < 0 ? Revision : revision, RequestedDenars, Collecting, AllCollected, Incident, AwaitingPayment,
            CollectionSettlementId, Villages, UsedIncidents, Journal, ProgressLogIndex, menu, finishEncounter,
            refreshPresentation, menuSettlementId, isFinalizing);
}

[ProtoContract]
public sealed class RevenueFarmingVillageState
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
public sealed class RevenueFarmingJournalState
{
    [ProtoMember(1)] public long TimeTicks { get; set; }
    [ProtoMember(2)] public TextObject Text { get; set; }
    [ProtoMember(3)] public TextObject TaskName { get; set; }
    [ProtoMember(4)] public int Progress { get; set; }
    [ProtoMember(5)] public int Range { get; set; }
    [ProtoMember(6)] public int Type { get; set; }
}
