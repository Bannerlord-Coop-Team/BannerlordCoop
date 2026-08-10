using ProtoBuf;

namespace GameInterface.Services.Kingdoms.Data;

[ProtoContract(SkipConstructor = true)]
public class SettlementClaimantDecisionSnapshotData
{
    [ProtoMember(1)]
    public string KingdomId { get; }

    [ProtoMember(2)]
    public int DecisionIndex { get; }

    [ProtoMember(3)]
    public SettlementClaimantCandidateData[] Candidates { get; }

    public SettlementClaimantDecisionSnapshotData(
        string kingdomId,
        int decisionIndex,
        SettlementClaimantCandidateData[] candidates)
    {
        KingdomId = kingdomId;
        DecisionIndex = decisionIndex;
        Candidates = candidates;
    }
}
