using GameInterface.Services.Issues.Interfaces;

namespace GameInterface.Services.Issues.Generic.Gates;

/// <summary>
/// Executes an <see cref="OwnershipGateSpec{TInstance}"/> - the shared boolean every
/// <see cref="GateKind.OwnerOnlyMethodGate"/> Harmony prefix reduces to. Callers still write their own
/// <c>[HarmonyPrefix]</c> shell (unavoidable Harmony boilerplate, same as every other gate primitive in this
/// design); this only removes the duplicated ownership-lookup logic each gate would otherwise repeat by hand.
///
/// Not usable at every gate site: a site with a non-boolean, out-parameter-carrying signature doesn't reduce to
/// a bare boolean gate cleanly and should keep its existing direct
/// <see cref="VillageNeedsToolsIssueOwnership.IsLocalPeerOwner"/> call instead.
/// </summary>
public static class OwnershipGateRunner
{
    public static bool IsOwner<TInstance>(OwnershipGateSpec<TInstance> spec, TInstance instance)
    {
        if (spec?.QuestGiverSelector == null || instance == null) return false;

        var questGiver = spec.QuestGiverSelector(instance);
        return VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(questGiver);
    }
}
