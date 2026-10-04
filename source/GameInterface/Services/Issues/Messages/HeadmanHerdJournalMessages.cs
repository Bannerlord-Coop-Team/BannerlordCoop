using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Messages;

internal readonly struct HeadmanHerdJournalAdded : IEvent
{
    public readonly HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest Quest;
    public readonly TextObject Text;
    public readonly bool HideInformation;

    public HeadmanHerdJournalAdded(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest quest,
        TextObject text, bool hideInformation)
    {
        Quest = quest;
        Text = text;
        HideInformation = hideInformation;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHeadmanHerdJournalAdded : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string OwnerId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly string QuestId;
    [ProtoMember(4)] public readonly int Index;
    [ProtoMember(5)] public readonly CampaignTime Time;
    [ProtoMember(6)] public readonly TextObject Text;
    [ProtoMember(7)] public readonly bool HideInformation;

    public NetworkHeadmanHerdJournalAdded(string ownerId, int generation, string questId,
        int index, CampaignTime time, TextObject text, bool hideInformation)
    {
        OwnerId = ownerId;
        Generation = generation;
        QuestId = questId;
        Index = index;
        Time = time;
        Text = text;
        HideInformation = hideInformation;
    }
}
