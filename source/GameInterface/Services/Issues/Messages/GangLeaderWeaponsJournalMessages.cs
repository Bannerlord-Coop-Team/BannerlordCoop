using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Messages;

using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

internal readonly struct GangLeaderWeaponsQuestEnded : IEvent
{
    public readonly Quest Quest;

    public GangLeaderWeaponsQuestEnded(Quest quest) => Quest = quest;
}

internal readonly struct GangLeaderWeaponsLogAdded : IEvent
{
    public readonly Quest Quest;
    public readonly JournalLog Log;
    public readonly bool HideInformation;

    public GangLeaderWeaponsLogAdded(Quest quest, JournalLog log, bool hideInformation)
    {
        Quest = quest;
        Log = log;
        HideInformation = hideInformation;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkGangLeaderWeaponsLog : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly string QuestId;
    [ProtoMember(4)] public readonly int Index;
    [ProtoMember(5)] public readonly CampaignTime Time;
    [ProtoMember(6)] public readonly TextObject Text;
    [ProtoMember(7)] public readonly bool HideInformation;

    public NetworkGangLeaderWeaponsLog(string giverId, int generation, string questId, int index,
        CampaignTime time, TextObject text, bool hideInformation)
    {
        GiverId = giverId;
        Generation = generation;
        QuestId = questId;
        Index = index;
        Time = time;
        Text = text;
        HideInformation = hideInformation;
    }
}

internal readonly struct GangLeaderWeaponsStateChanged : IEvent
{
    public readonly Quest Quest;
    public readonly GangLeaderWeaponsAction Action;

    public GangLeaderWeaponsStateChanged(Quest quest, GangLeaderWeaponsAction action)
    {
        Quest = quest;
        Action = action;
    }
}
