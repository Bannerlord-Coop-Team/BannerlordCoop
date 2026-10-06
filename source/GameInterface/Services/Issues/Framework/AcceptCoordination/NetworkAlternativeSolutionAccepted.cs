using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAlternativeSolutionAccepted : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly TroopRosterElementData[] SentTroops;

    [ProtoMember(4)]
    public readonly AlternativeSolutionVanillaState State;

    [ProtoMember(5)]
    public readonly byte[] Captured;

    public NetworkAlternativeSolutionAccepted(
        string issueOwnerId,
        string issueId,
        TroopRosterElementData[] sentTroops,
        AlternativeSolutionVanillaState state,
        byte[] captured)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        SentTroops = sentTroops;
        State = state;
        Captured = captured;
    }
}
