using System.Collections.Generic;

namespace GameInterface.Services.Issues.Generic;

public interface IAppliedPendingQuestFailConsequenceTracker
{
    bool TryMarkApplied(long obligationId);
    void ClearAll();
    void Restore(long obligationId);
    IReadOnlyCollection<long> Snapshot();
}

internal sealed class AppliedPendingQuestFailConsequenceTracker : IAppliedPendingQuestFailConsequenceTracker
{
    private readonly HashSet<long> appliedObligationIds = new();

    public bool TryMarkApplied(long obligationId) => appliedObligationIds.Add(obligationId);

    public void ClearAll() => appliedObligationIds.Clear();

    public void Restore(long obligationId) => appliedObligationIds.Add(obligationId);

    public IReadOnlyCollection<long> Snapshot() => appliedObligationIds;
}
