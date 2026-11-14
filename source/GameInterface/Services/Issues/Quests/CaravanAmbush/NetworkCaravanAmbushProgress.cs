using Common.Messaging;
using ProtoBuf;
using GameInterface.Services.Issues.Framework.Finalization;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkCaravanAmbushProgress : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly string CaravanId;
    [ProtoMember(5)] public readonly string BanditId;
    [ProtoMember(6)] public readonly bool Saved;
    [ProtoMember(7)] public readonly int Warnings;
    [ProtoMember(8)] public readonly IssueJournalEntry[] Journal;

    public NetworkCaravanAmbushProgress(string ownerId, string issueId, string controllerId,
        string caravanId, string banditId, bool saved, int warnings, IssueJournalEntry[] journal)
    {
        IssueOwnerId = ownerId;
        IssueId = issueId;
        ControllerId = controllerId;
        CaravanId = caravanId;
        BanditId = banditId;
        Saved = saved;
        Warnings = warnings;
        Journal = journal;
    }
}
