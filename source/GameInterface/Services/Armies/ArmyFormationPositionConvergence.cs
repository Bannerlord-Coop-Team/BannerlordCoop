using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies;

/// <summary>Decides when a gathering player's army-leader position needs convergence.</summary>
internal interface IArmyFormationPositionConvergence
{
    bool CanReport(ArmyFormationPositionState state);

    bool ShouldReport(
        ArmyFormationPositionState state,
        bool hasPreviousPosition,
        CampaignVec2 previousPosition);

    bool ShouldApply(ArmyFormationPositionState state, CampaignVec2 reportedPosition);
}

/// <summary>Position and non-distance formation gates for one player-led army.</summary>
internal readonly struct ArmyFormationPositionState
{
    public string LeaderPartyId { get; }
    public CampaignVec2 Position { get; }
    public bool IsActive { get; }
    public bool IsControlled { get; }
    public bool IsArmyLeader { get; }
    public bool IsAttached { get; }
    public bool IsInMapEvent { get; }
    public bool IsInSettlement { get; }
    public bool IsCurrentlyAtSea { get; }
    public bool HasConvergingMember { get; }
    public bool HasNearbyConvergingMember { get; }
    public float AttachmentDistanceSquared { get; }

    public ArmyFormationPositionState(
        string leaderPartyId,
        CampaignVec2 position,
        bool isActive,
        bool isControlled,
        bool isArmyLeader,
        bool isAttached,
        bool isInMapEvent,
        bool isInSettlement,
        bool isCurrentlyAtSea,
        bool hasConvergingMember,
        bool hasNearbyConvergingMember,
        float attachmentDistanceSquared)
    {
        LeaderPartyId = leaderPartyId;
        Position = position;
        IsActive = isActive;
        IsControlled = isControlled;
        IsArmyLeader = isArmyLeader;
        IsAttached = isAttached;
        IsInMapEvent = isInMapEvent;
        IsInSettlement = isInSettlement;
        IsCurrentlyAtSea = isCurrentlyAtSea;
        HasConvergingMember = hasConvergingMember;
        HasNearbyConvergingMember = hasNearbyConvergingMember;
        AttachmentDistanceSquared = attachmentDistanceSquared;
    }
}

/// <inheritdoc cref="IArmyFormationPositionConvergence"/>
internal sealed class ArmyFormationPositionConvergence : IArmyFormationPositionConvergence
{
    // Bound formation-only drift below Army.Tick's unchanged attachment distance without sending every frame.
    internal const float ReportDistanceSquared = 0.0625f;

    public bool ShouldReport(
        ArmyFormationPositionState state,
        bool hasPreviousPosition,
        CampaignVec2 previousPosition)
    {
        if (!CanReport(state)) return false;
        if (!hasPreviousPosition) return true;
        if (previousPosition.IsOnLand != state.Position.IsOnLand) return true;

        return previousPosition.DistanceSquared(state.Position) >= ReportDistanceSquared;
    }

    public bool ShouldApply(ArmyFormationPositionState state, CampaignVec2 reportedPosition)
    {
        return IsEligible(state) &&
            state.HasNearbyConvergingMember &&
            state.AttachmentDistanceSquared > 0f &&
            reportedPosition.ToVec2().IsValid &&
            reportedPosition.IsOnLand != state.IsCurrentlyAtSea &&
            reportedPosition != state.Position &&
            reportedPosition.DistanceSquared(state.Position) <= state.AttachmentDistanceSquared;
    }

    public bool CanReport(ArmyFormationPositionState state) =>
        IsEligible(state) && state.HasNearbyConvergingMember;

    private static bool IsEligible(ArmyFormationPositionState state)
    {
        return !string.IsNullOrEmpty(state.LeaderPartyId) &&
            state.Position.ToVec2().IsValid &&
            state.Position.IsOnLand != state.IsCurrentlyAtSea &&
            state.IsActive &&
            state.IsControlled &&
            state.IsArmyLeader &&
            !state.IsAttached &&
            !state.IsInMapEvent &&
            !state.IsInSettlement &&
            state.HasConvergingMember;
    }
}
