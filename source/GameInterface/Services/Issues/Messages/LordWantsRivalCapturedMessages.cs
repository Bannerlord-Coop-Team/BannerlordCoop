using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Messages;

internal readonly struct RivalCapturedIssueCreated : IEvent
{
    public readonly LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue Issue;

    public RivalCapturedIssueCreated(LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue issue)
    {
        Issue = issue;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRivalCapturedIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string TargetId;
    [ProtoMember(3)] public readonly string IssueId;
    [ProtoMember(4)] public readonly int Generation;
    [ProtoMember(5)] public readonly CampaignTime CreationTime;
    [ProtoMember(6)] public readonly CampaignTime DueTime;

    public NetworkRivalCapturedIssueCreated(string giverId, string targetId, string issueId, int generation,
        CampaignTime creationTime, CampaignTime dueTime)
    {
        GiverId = giverId;
        TargetId = targetId;
        IssueId = issueId;
        Generation = generation;
        CreationTime = creationTime;
        DueTime = dueTime;
    }
}

internal enum RivalCapturedChoice
{
    HearCounterOffer,
    AcceptCounterOffer,
    DeliverToGiver,
    DeliverToAgent,
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestRivalCapturedChoice : ICommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly RivalCapturedChoice Choice;

    public RequestRivalCapturedChoice(string giverId, int generation, RivalCapturedChoice choice)
    {
        GiverId = giverId;
        Generation = generation;
        Choice = choice;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RivalCapturedLog
{
    [ProtoMember(1)] public readonly CampaignTime Time;
    [ProtoMember(2)] public readonly TextObject Text;

    public RivalCapturedLog(JournalLog log)
    {
        Time = log.LogTime;
        Text = log.LogText;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RivalCapturedQuestState
{
    [ProtoMember(1)] public readonly bool FirstCounterOfferMade;
    [ProtoMember(2)] public readonly int GiverRelationChange;
    [ProtoMember(3)] public readonly RivalCapturedLog[] Logs;

    public RivalCapturedQuestState(bool firstCounterOfferMade, int giverRelationChange, RivalCapturedLog[] logs)
    {
        FirstCounterOfferMade = firstCounterOfferMade;
        GiverRelationChange = giverRelationChange;
        Logs = logs;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RivalCapturedAcceptFields
{
    [ProtoMember(1)] public readonly string ControllerId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly CampaignTime DueTime;
    [ProtoMember(4)] public readonly RivalCapturedQuestState State;

    public RivalCapturedAcceptFields(string controllerId, int generation, CampaignTime dueTime, RivalCapturedQuestState state)
    {
        ControllerId = controllerId;
        Generation = generation;
        DueTime = dueTime;
        State = state;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRivalCapturedProgress : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly RivalCapturedQuestState State;

    public NetworkRivalCapturedProgress(string giverId, int generation, RivalCapturedQuestState state)
    {
        GiverId = giverId;
        Generation = generation;
        State = state;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRivalCapturedTraitProgress : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string HeroId;
    [ProtoMember(2)] public readonly int HonorXp;

    public NetworkRivalCapturedTraitProgress(string heroId, int honorXp)
    {
        HeroId = heroId;
        HonorXp = honorXp;
    }
}
