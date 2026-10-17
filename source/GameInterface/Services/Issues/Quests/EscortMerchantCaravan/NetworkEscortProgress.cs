using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan;

[ProtoContract(SkipConstructor = true)]
internal sealed class NetworkEscortProgress : IServerToClientCommand
{
    [ProtoMember(1)] public string GiverId;
    [ProtoMember(2)] public string IssueId;
    [ProtoMember(3)] public string ControllerId;
    [ProtoMember(4)] public string CaravanId;
    [ProtoMember(5)] public string BanditId;
    [ProtoMember(6)] public string OtherBanditId;
    [ProtoMember(7)] public string[] VisitedIds;
    [ProtoMember(8)] public int Days;
    [ProtoMember(9)] public bool Warned;
    [ProtoMember(10)] public byte[] Journal;
    [ProtoMember(11)] public string[] TrackedParties;
    [ProtoMember(12)] public string[] TrackedSettlements;
}
