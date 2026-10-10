using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Hideouts.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHideoutRaidEntryRequest : ICommand
{
    [ProtoMember(1)] public readonly string RequestId;
    [ProtoMember(2)] public readonly string SettlementId;
    [ProtoMember(3)] public readonly bool IsDirectAssault;
    [ProtoMember(4)] public readonly bool QueryOnly;
    [ProtoMember(5)] public readonly HideoutTroopSelectionEntry[] Troops;

    public NetworkHideoutRaidEntryRequest(string requestId, string settlementId, bool isDirectAssault,
        bool queryOnly, HideoutTroopSelectionEntry[] troops)
    {
        RequestId = requestId;
        SettlementId = settlementId;
        IsDirectAssault = isDirectAssault;
        QueryOnly = queryOnly;
        Troops = troops;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHideoutRaidEntryReply : ICommand
{
    [ProtoMember(1)] public readonly string RequestId;
    [ProtoMember(2)] public readonly bool Accepted;
    [ProtoMember(3)] public readonly string MapEventId;
    [ProtoMember(4)] public readonly int RemainingTroops;
    [ProtoMember(5)] public readonly bool IsDirectAssault;
    [ProtoMember(6)] public readonly bool IsJoining;
    [ProtoMember(7)] public readonly string Reason;
    [ProtoMember(8)] public readonly bool IsAdmitted;

    public NetworkHideoutRaidEntryReply(string requestId, bool accepted, string mapEventId,
        int remainingTroops, bool isDirectAssault, bool isJoining, string reason = null, bool isAdmitted = false)
    {
        RequestId = requestId;
        Accepted = accepted;
        MapEventId = mapEventId;
        RemainingTroops = remainingTroops;
        IsDirectAssault = isDirectAssault;
        IsJoining = isJoining;
        Reason = reason;
        IsAdmitted = isAdmitted;
    }
}
