namespace GameInterface.Services.Issues.Generic.Gates;

public enum GateKind
{
    OwnerOnlyMethodGate,

    SideEffectServerOnly,

    // Reserved - no real gate matches this shape yet; don't build generic machinery for it.
    StateFlagSynced,

    GateAndInject
}
