using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>The troops the server holds for a player's quest picker after a commit, so the picker drops rejected ones.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAlternativeSolutionSelection : ICommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly TroopRosterElementData[] Troops;

    public NetworkAlternativeSolutionSelection(string issueOwnerId, string issueId, TroopRosterElementData[] troops)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Troops = troops;
    }
}
