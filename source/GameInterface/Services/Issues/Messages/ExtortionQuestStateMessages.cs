using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Messages;

internal readonly struct ExtortionTraitXpChanged : IEvent
{
    public readonly Hero Hero;
    public readonly TraitObject Trait;
    public readonly int Xp;

    public ExtortionTraitXpChanged(Hero hero, TraitObject trait, int xp)
    {
        Hero = hero;
        Trait = trait;
        Xp = xp;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkExtortionTraitXp : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string HeroId;
    [ProtoMember(2)] public readonly string TraitId;
    [ProtoMember(3)] public readonly int Xp;
    [ProtoMember(4)] public readonly int Level;

    public NetworkExtortionTraitXp(string heroId, string traitId, int xp, int level)
    {
        HeroId = heroId;
        TraitId = traitId;
        Xp = xp;
        Level = level;
    }
}

internal readonly struct ExtortionQuestChanged : IEvent
{
    public readonly ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest Quest;
    public readonly bool StartAmbush;

    public ExtortionQuestChanged(ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest quest, bool startAmbush = false)
    {
        Quest = quest;
        StartAmbush = startAmbush;
    }
}

internal readonly struct ExtortionAlternativeChanged : IEvent
{
    public readonly ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue Issue;
    public readonly IssueBase.IssueUpdateDetails Status;

    public ExtortionAlternativeChanged(ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue issue,
        IssueBase.IssueUpdateDetails status = IssueBase.IssueUpdateDetails.None)
    {
        Issue = issue;
        Status = status;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkExtortionAlternativeState : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly bool EffectsResolved;
    [ProtoMember(4)] public readonly ExtortionJournalEntry[] Journal;
    [ProtoMember(5)] public readonly IssueBase.IssueUpdateDetails Status;

    public NetworkExtortionAlternativeState(string giverId, string issueId, bool effectsResolved,
        ExtortionJournalEntry[] journal, IssueBase.IssueUpdateDetails status)
    {
        GiverId = giverId;
        IssueId = issueId;
        EffectsResolved = effectsResolved;
        Journal = journal;
        Status = status;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ExtortionJournalEntry
{
    [ProtoMember(1)] public readonly CampaignTime Time;
    [ProtoMember(2)] public readonly TextObject Text;
    [ProtoMember(3)] public readonly TextObject Task;
    [ProtoMember(4)] public readonly int Progress;
    [ProtoMember(5)] public readonly int Range;
    [ProtoMember(6)] public readonly LogType Type;

    public ExtortionJournalEntry(JournalLog entry)
    {
        Time = entry.LogTime;
        Text = entry.LogText;
        Task = entry.TaskName;
        Progress = entry.CurrentProgress;
        Range = entry.Range;
        Type = entry.Type;
    }

    public JournalLog ToJournalLog() => new JournalLog(Time, Text, Task, Progress, Range, Type);
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkExtortionQuestState : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string QuestId;
    [ProtoMember(3)] public readonly int State;
    [ProtoMember(4)] public readonly string DeserterPartyId;
    [ProtoMember(5)] public readonly string DefenderPartyId;
    [ProtoMember(6)] public readonly CampaignTime RunAwayDueTime;
    [ProtoMember(7)] public readonly bool BattleFinalized;
    [ProtoMember(8)] public readonly bool AwayWarningSent;
    [ProtoMember(9)] public readonly ExtortionJournalEntry[] Journal;
    [ProtoMember(10)] public readonly bool StartAmbush;
    [ProtoMember(11)] public readonly string AmbushMapEventId;

    public NetworkExtortionQuestState(string giverId, string questId, int state, string deserterPartyId,
        string defenderPartyId, CampaignTime runAwayDueTime, bool battleFinalized, bool awayWarningSent,
        ExtortionJournalEntry[] journal, bool startAmbush = false, string ambushMapEventId = null)
    {
        GiverId = giverId;
        QuestId = questId;
        State = state;
        DeserterPartyId = deserterPartyId;
        DefenderPartyId = defenderPartyId;
        RunAwayDueTime = runAwayDueTime;
        BattleFinalized = battleFinalized;
        AwayWarningSent = awayWarningSent;
        Journal = journal;
        StartAmbush = startAmbush;
        AmbushMapEventId = ambushMapEventId;
    }
}
