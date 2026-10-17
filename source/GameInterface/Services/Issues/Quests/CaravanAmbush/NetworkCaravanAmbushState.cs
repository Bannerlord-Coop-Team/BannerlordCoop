using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCaravanAmbushState : IServerToClientCommand
{
    [ProtoMember(1)] public string IssueOwnerId { get; }
    [ProtoMember(2)] public string IssueId { get; }
    [ProtoMember(3)] public string ControllerId { get; }
    [ProtoMember(4)] public string CaravanId { get; }
    [ProtoMember(5)] public string BanditId { get; }
    [ProtoMember(6)] public bool Saved { get; }
    [ProtoMember(7)] public int Warnings { get; }
    [ProtoMember(8)] public string[] Journal { get; }

    public NetworkCaravanAmbushState(string ownerId, string issueId, string controllerId,
        string caravanId, string banditId, bool saved, int warnings, string[] journal)
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
