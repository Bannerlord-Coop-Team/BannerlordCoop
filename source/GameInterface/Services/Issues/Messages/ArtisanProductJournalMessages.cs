using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

public readonly struct ArtisanProductJournalChanged : IEvent
{
    public readonly ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue Issue;

    public ArtisanProductJournalChanged(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue issue)
        => Issue = issue;
}

[ProtoContract(SkipConstructor = true)]
public readonly struct ArtisanProductLogEntry
{
    [ProtoMember(1)] public readonly CampaignTime Time;
    [ProtoMember(2)] public readonly byte[] Text;
    [ProtoMember(3)] public readonly byte[] TaskName;
    [ProtoMember(4)] public readonly int Progress;
    [ProtoMember(5)] public readonly int Range;
    [ProtoMember(6)] public readonly int Type;

    public ArtisanProductLogEntry(CampaignTime time, byte[] text, byte[] taskName, int progress, int range, int type)
    {
        Time = time;
        Text = text;
        TaskName = taskName;
        Progress = progress;
        Range = range;
        Type = type;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanProductJournal : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly ArtisanProductLogEntry[] IssueEntries;
    [ProtoMember(4)] public readonly ArtisanProductLogEntry[] QuestEntries;
    [ProtoMember(5)] public readonly int DeliveryLogIndex;
    [ProtoMember(6)] public readonly int Delivered;
    [ProtoMember(7)] public readonly bool MerchantRefused;
    [ProtoMember(8)] public readonly IssueBase.IssueUpdateDetails IssueStatus;
    [ProtoMember(9)] public readonly QuestBase.QuestCompleteDetails QuestStatus;
    [ProtoMember(10)] public readonly bool MerchantOfferGiven;
    [ProtoMember(11)] public readonly bool IssueEffectsResolved;

    public NetworkArtisanProductJournal(string giverId, int generation, ArtisanProductLogEntry[] issueEntries,
        ArtisanProductLogEntry[] questEntries, int deliveryLogIndex, int delivered, bool merchantRefused,
        IssueBase.IssueUpdateDetails issueStatus, QuestBase.QuestCompleteDetails questStatus, bool merchantOfferGiven,
        bool issueEffectsResolved)
    {
        GiverId = giverId;
        Generation = generation;
        IssueEntries = issueEntries;
        QuestEntries = questEntries;
        DeliveryLogIndex = deliveryLogIndex;
        Delivered = delivered;
        MerchantRefused = merchantRefused;
        IssueStatus = issueStatus;
        QuestStatus = questStatus;
        MerchantOfferGiven = merchantOfferGiven;
        IssueEffectsResolved = issueEffectsResolved;
    }
}
