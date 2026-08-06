using GameInterface.Services.Issues.Interfaces;

namespace GameInterface.Services.Issues.Generic.Gates;

public static class OwnershipGateRunner
{
    public static bool IsOwner<TInstance>(OwnershipGateSpec<TInstance> spec, TInstance instance)
    {
        if (spec?.QuestGiverSelector == null || instance == null) return false;

        var questGiver = spec.QuestGiverSelector(instance);
        return IssueOwnershipRegistry.IsLocalPeerOwner(questGiver);
    }
}
