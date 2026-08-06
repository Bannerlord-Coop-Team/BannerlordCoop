namespace GameInterface.Services.Issues.Generic.Gates;

/// <summary>
/// The known ownership-gate shapes across this codebase's Harmony gate entries in
/// <c>Patches/*OwnershipGatePatch(es).cs</c>.
/// <see cref="StateFlagSynced"/> is reserved - no shipped instances currently match its intended shape, and no
/// generic machinery exists for it yet. Kept in the enum only so a future real instance has a name to slot into.
/// </summary>
public enum GateKind
{
    /// <summary>Gates an entire method body to the recorded local-peer owner - e.g.
    /// <c>VillageNeedsCraftingMaterialsQuestOwnershipGatePatch</c>,
    /// <c>VillageNeedsCraftingMaterialsAlternativeSolutionOwnershipGatePatch</c>.</summary>
    OwnerOnlyMethodGate,

    /// <summary>Gates a side-effect-producing method to the server/host process only (as opposed to a specific
    /// owner Hero).</summary>
    SideEffectServerOnly,

    /// <summary>RESERVED - no known real gates match this shape (an "already-synced field, override the
    /// getter, no body-skip needed" gate). Do not implement generic machinery for it.</summary>
    StateFlagSynced,

    /// <summary>See <see cref="GateAndInjectDescriptor{TQuest}"/>.</summary>
    GateAndInject
}
