using GameInterface.Services.SiegeEvents.Commands;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

public class DefenderSiegeFixtureContractTests
{
    [Fact]
    public void HasExactControllerIds_RequiresTheTwoExpectedConnectedControllers()
    {
        Assert.True(DefenderSiegeFixtureContract.HasExactControllerIds(
            new[] { "testclient2", "testclient" },
            new[] { "testclient", "testclient2" }));
        Assert.False(DefenderSiegeFixtureContract.HasExactControllerIds(
            new[] { "testclient", "testclient" },
            new[] { "testclient", "testclient2" }));
        Assert.False(DefenderSiegeFixtureContract.HasExactControllerIds(
            new[] { "testclient", "testclient2", "other" },
            new[] { "testclient", "testclient2" }));
    }

    [Fact]
    public void IsCleanForCapture_AllowsRestorableOriginalMovementAndRejectsAmbiguousPartyState()
    {
        Assert.True(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState()));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(hasMapEvent: true)));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(hasBesiegerCamp: true)));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(isAtSea: true)));
        Assert.True(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(isHolding: false)));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(isTransitionInProgress: true)));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(hasArmy: true)));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(hasAttachedTo: true)));
        Assert.False(DefenderSiegeFixtureContract.IsCleanForCapture(CreateState(hasAttachedParties: true)));
    }

    [Fact]
    public void IsStaged_RequiresTheDefenderPartyInsideDanusticaWithoutSiegeState()
    {
        Assert.True(DefenderSiegeFixtureContract.IsStaged(
            CreateState(currentSettlementId: "town_ES1"), "town_ES1"));
        Assert.False(DefenderSiegeFixtureContract.IsStaged(
            CreateState(currentSettlementId: "town_ES1", hasBesiegerCamp: true), "town_ES1"));
        Assert.False(DefenderSiegeFixtureContract.IsStaged(
            CreateState(currentSettlementId: "town_ES2"), "town_ES1"));
    }

    [Fact]
    public void IsRestored_RequiresTheCapturedPartyStateExactly()
    {
        DefenderFixturePartyState captured = CreateState(
            currentSettlementId: "town_ES2",
            lastVisitedSettlementId: "town_ES2",
            positionX: 12f,
            bearingY: 4f);

        Assert.True(DefenderSiegeFixtureContract.IsRestored(captured, captured));
        Assert.False(DefenderSiegeFixtureContract.IsRestored(captured,
            CreateState(
                currentSettlementId: "town_ES2",
                lastVisitedSettlementId: "town_ES2",
                positionX: 12f,
                bearingY: 5f)));
    }

    [Fact]
    public void IsPreAssaultReady_RequiresTopologyAndTheRoleLocalParty()
    {
        Assert.True(DefenderSiegeFixtureContract.IsPreAssaultReady(
            fixtureStaged: true,
            connectionReady: true,
            expectedPlayersResolved: true,
            noMapEvent: true,
            noBesiegerCamp: true,
            insideSettlement: true,
            localPartyReady: true));
        Assert.False(DefenderSiegeFixtureContract.IsPreAssaultReady(
            fixtureStaged: true,
            connectionReady: true,
            expectedPlayersResolved: true,
            noMapEvent: true,
            noBesiegerCamp: true,
            insideSettlement: true,
            localPartyReady: false));
    }

    [Fact]
    public void CanEnterSettlementWithoutOwnerVisit_RejectsAnOwnerLeader()
    {
        Assert.True(DefenderSiegeFixtureContract.CanEnterSettlementWithoutOwnerVisit(false));
        Assert.False(DefenderSiegeFixtureContract.CanEnterSettlementWithoutOwnerVisit(true));
    }

    [Fact]
    public void IsConnectionReadinessSatisfied_RequiresServerIdentityButOnlyAnAuthoritativeCountOnClients()
    {
        Assert.True(DefenderSiegeFixtureContract.IsConnectionReadinessSatisfied(
            isServer: true,
            connectedPlayerCount: 2,
            connectedControllersExact: true));
        Assert.False(DefenderSiegeFixtureContract.IsConnectionReadinessSatisfied(
            isServer: true,
            connectedPlayerCount: 2,
            connectedControllersExact: false));
        Assert.True(DefenderSiegeFixtureContract.IsConnectionReadinessSatisfied(
            isServer: false,
            connectedPlayerCount: 2,
            connectedControllersExact: false));
        Assert.False(DefenderSiegeFixtureContract.IsConnectionReadinessSatisfied(
            isServer: false,
            connectedPlayerCount: 1,
            connectedControllersExact: false));
    }

#if DEBUG
    [Fact]
    public void CaptiveRosterBaseline_RequiresTheNormalParkedPlayerState()
    {
        Assert.True(CaptiveRosterBaseline());
        Assert.False(CaptiveRosterBaseline(heroStateIsPrisoner: false));
        Assert.False(CaptiveRosterBaseline(captorPartyIsActive: false));
        Assert.False(CaptiveRosterBaseline(partyActive: true));
        Assert.False(CaptiveRosterBaseline(partyVisible: true));
        Assert.False(CaptiveRosterBaseline(partyHasVisual: true));
        Assert.False(CaptiveRosterBaseline(partyPrisonerCount: 1));
        Assert.False(CaptiveRosterBaseline(captorHeroWoundedNumber: 1));
        Assert.False(CaptiveRosterBaseline(captorHeroXp: 1));
        Assert.False(CaptiveRosterBaseline(heroStaysInSettlement: true));
    }

    [Fact]
    public void RosterNormalizationRetry_RejectsAnAmbiguousPreviousAttempt()
    {
        Assert.True(DefenderRosterFixtureContract.IsNormalizationRetryable(
            isNormalized: false,
            normalizationUnsafe: false));
        Assert.False(DefenderRosterFixtureContract.IsNormalizationRetryable(
            isNormalized: true,
            normalizationUnsafe: false));
        Assert.False(DefenderRosterFixtureContract.IsNormalizationRetryable(
            isNormalized: false,
            normalizationUnsafe: true));
    }

    [Fact]
    public void NormalizedRosterState_RequiresTheCurrentReleasedRoster()
    {
        Assert.True(DefenderRosterFixtureContract.CanReportNormalizedSuccess(
            isNormalized: true,
            normalizationUnsafe: false,
            normalizedStateCurrent: true));
        Assert.False(DefenderRosterFixtureContract.CanReportNormalizedSuccess(
            isNormalized: true,
            normalizationUnsafe: false,
            normalizedStateCurrent: false));
        Assert.False(DefenderRosterFixtureContract.CanReportNormalizedSuccess(
            isNormalized: true,
            normalizationUnsafe: true,
            normalizedStateCurrent: true));
    }

    [Fact]
    public void RestoreRosterState_AllowsAnAlreadyRecapturedSnapshotReplayForRetry()
    {
        Assert.True(DefenderRosterFixtureContract.CanRestoreCapturedPlayer(
            capturedBaseline: true,
            recapturedSnapshotReplaySafe: false,
            releasedForRestoration: false));
        Assert.True(DefenderRosterFixtureContract.CanRestoreCapturedPlayer(
            capturedBaseline: false,
            recapturedSnapshotReplaySafe: true,
            releasedForRestoration: false));
        Assert.True(DefenderRosterFixtureContract.CanRestoreCapturedPlayer(
            capturedBaseline: false,
            recapturedSnapshotReplaySafe: false,
            releasedForRestoration: true));
        Assert.False(DefenderRosterFixtureContract.CanRestoreCapturedPlayer(
            capturedBaseline: false,
            recapturedSnapshotReplaySafe: false,
            releasedForRestoration: false));
    }

    [Fact]
    public void RestoreFailure_DoesNotDisableRetriesWhenSnapshotReplayIsPending()
    {
        Assert.False(DefenderRosterFixtureContract.RequiresRestoreFailureEscalation(
            capturedBaseline: false,
            recapturedSnapshotReplaySafe: true,
            releasedForRestoration: false));
        Assert.False(DefenderRosterFixtureContract.RequiresRestoreFailureEscalation(
            capturedBaseline: false,
            recapturedSnapshotReplaySafe: false,
            releasedForRestoration: true));
        Assert.True(DefenderRosterFixtureContract.RequiresRestoreFailureEscalation(
            capturedBaseline: false,
            recapturedSnapshotReplaySafe: false,
            releasedForRestoration: false));
    }

    [Fact]
    public void RecapturedSnapshotReplay_RequiresASafeCapturedLifecycleState()
    {
        Assert.True(DefenderRosterFixtureContract.IsRecapturedSnapshotReplaySafe(
            normallyRecaptured: true,
            behaviorRestorable: true,
            attackProtectionsCurrent: true));
        Assert.False(DefenderRosterFixtureContract.IsRecapturedSnapshotReplaySafe(
            normallyRecaptured: false,
            behaviorRestorable: true,
            attackProtectionsCurrent: true));
        Assert.False(DefenderRosterFixtureContract.IsRecapturedSnapshotReplaySafe(
            normallyRecaptured: true,
            behaviorRestorable: false,
            attackProtectionsCurrent: true));
        Assert.False(DefenderRosterFixtureContract.IsRecapturedSnapshotReplaySafe(
            normallyRecaptured: true,
            behaviorRestorable: true,
            attackProtectionsCurrent: false));
    }

    [Fact]
    public void NormalizedRosterPlayer_RequiresReadinessAndRestoreSafety()
    {
        Assert.True(DefenderRosterFixtureContract.CanAcceptNormalizedPlayer(
            releasedForReadiness: true,
            releasedRestorable: true));
        Assert.False(DefenderRosterFixtureContract.CanAcceptNormalizedPlayer(
            releasedForReadiness: true,
            releasedRestorable: false));
        Assert.False(DefenderRosterFixtureContract.CanAcceptNormalizedPlayer(
            releasedForReadiness: false,
            releasedRestorable: true));
    }

    [Fact]
    public void NormalizedRosterRestorePreflight_RequiresCurrentBehaviorAndProtections()
    {
        Assert.True(DefenderRosterFixtureContract.IsReleasedRosterRestorable(
            releasedForRestoration: true,
            behaviorRestorable: true,
            attackProtectionsCurrent: true));
        Assert.False(DefenderRosterFixtureContract.IsReleasedRosterRestorable(
            releasedForRestoration: true,
            behaviorRestorable: false,
            attackProtectionsCurrent: true));
        Assert.False(DefenderRosterFixtureContract.IsReleasedRosterRestorable(
            releasedForRestoration: true,
            behaviorRestorable: true,
            attackProtectionsCurrent: false));
    }

    [Fact]
    public void ReleasedRosterReadiness_RequiresTheNormalVisibilityLifecycle()
    {
        Assert.True(ReleasedRosterReadiness());
        Assert.False(ReleasedRosterReadiness(heroStateIsActive: false));
        Assert.False(ReleasedRosterReadiness(partyVisible: false));
        Assert.False(ReleasedRosterReadiness(partyHasVisual: false));
        Assert.False(ReleasedRosterReadiness(partyLeaderIsHero: false));
        Assert.False(ReleasedRosterReadiness(partyHeroMemberCount: 0));
    }

    [Fact]
    public void ReleasedRosterRestore_RejectsAnyStateThatCannotReenterAuthoritativeCaptivity()
    {
        Assert.True(ReleasedRosterRestore());
        Assert.False(ReleasedRosterRestore(partyHasCurrentSettlement: true));
        Assert.False(ReleasedRosterRestore(partyHasMapEvent: true));
        Assert.False(ReleasedRosterRestore(partyHasAttachedTo: true));
        Assert.False(ReleasedRosterRestore(partyMemberCount: 2));
        Assert.False(ReleasedRosterRestore(partyPrisonerCount: 1));
        Assert.False(ReleasedRosterRestore(captorPartyIsActive: false));
        Assert.False(ReleasedRosterRestore(captorHeroPrisonerCount: 1));
    }

    [Fact]
    public void NormalizationSuccess_RejectsAReleasedPartyWithAdditionalMembers()
    {
        Assert.True(ReleasedRosterReadiness(partyHeroMemberCount: 1));
        Assert.False(ReleasedRosterRestore(partyMemberCount: 2));
        Assert.False(DefenderRosterFixtureContract.CanAcceptNormalizedPlayer(
            releasedForReadiness: ReleasedRosterReadiness(partyHeroMemberCount: 1),
            releasedRestorable: ReleasedRosterRestore(partyMemberCount: 2)));
    }

    [Fact]
    public void AttackProtectionRestore_RequiresBothPartyAndFactionProtectionSnapshots()
    {
        Assert.True(DefenderRosterFixtureContract.HasExactAttackProtectionRestoration(
            partyProtectionsRestored: true,
            factionProtectionsRestored: true));
        Assert.False(DefenderRosterFixtureContract.HasExactAttackProtectionRestoration(
            partyProtectionsRestored: true,
            factionProtectionsRestored: false));
        Assert.False(DefenderRosterFixtureContract.HasExactAttackProtectionRestoration(
            partyProtectionsRestored: false,
            factionProtectionsRestored: true));
    }

    [Fact]
    public void CapturedAttackProtectionState_RequiresEveryOriginalProtection()
    {
        Assert.True(DefenderRosterFixtureContract.IsCapturedAttackProtectionStateCurrent(
            originalPartyProtectionsCurrent: true,
            onlyOriginalAndReleaseProtections: true,
            hasAtMostOneReleaseProtection: true,
            factionProtectionsCurrent: true));
        Assert.False(DefenderRosterFixtureContract.IsCapturedAttackProtectionStateCurrent(
            originalPartyProtectionsCurrent: false,
            onlyOriginalAndReleaseProtections: true,
            hasAtMostOneReleaseProtection: true,
            factionProtectionsCurrent: true));
        Assert.False(DefenderRosterFixtureContract.IsCapturedAttackProtectionStateCurrent(
            originalPartyProtectionsCurrent: true,
            onlyOriginalAndReleaseProtections: false,
            hasAtMostOneReleaseProtection: true,
            factionProtectionsCurrent: true));
        Assert.False(DefenderRosterFixtureContract.IsCapturedAttackProtectionStateCurrent(
            originalPartyProtectionsCurrent: true,
            onlyOriginalAndReleaseProtections: true,
            hasAtMostOneReleaseProtection: false,
            factionProtectionsCurrent: true));
        Assert.False(DefenderRosterFixtureContract.IsCapturedAttackProtectionStateCurrent(
            originalPartyProtectionsCurrent: true,
            onlyOriginalAndReleaseProtections: true,
            hasAtMostOneReleaseProtection: true,
            factionProtectionsCurrent: false));
    }

    [Fact]
    public void CaptiveRosterState_TracksTheCaptorPositionAndPreservesTheOtherCapturedState()
    {
        DefenderFixturePartyState original = CreateState(
            currentSettlementId: "town_ES2",
            lastVisitedSettlementId: "town_ES2",
            positionX: 12f,
            positionY: 4f,
            bearingY: 6f);
        DefenderFixturePartyState current = CreateState(
            currentSettlementId: "town_ES2",
            lastVisitedSettlementId: "town_ES2",
            positionX: 24f,
            positionY: 8f,
            positionIsOnLand: false,
            bearingY: 6f);

        Assert.True(DefenderRosterFixtureContract.IsCapturedPartyStateCurrent(
            original,
            current,
            captorPositionX: 24f,
            captorPositionY: 8f,
            captorPositionIsOnLand: false));
        Assert.False(DefenderRosterFixtureContract.IsCapturedPartyStateCurrent(
            original,
            current,
            captorPositionX: 25f,
            captorPositionY: 8f,
            captorPositionIsOnLand: false));
        Assert.False(DefenderRosterFixtureContract.IsCapturedPartyStateCurrent(
            original,
            CreateState(
                currentSettlementId: "town_ES2",
                lastVisitedSettlementId: "town_ES2",
                positionX: 24f,
                positionY: 8f,
                positionIsOnLand: false,
                bearingY: 7f),
            captorPositionX: 24f,
            captorPositionY: 8f,
            captorPositionIsOnLand: false));
    }

    [Fact]
    public void UncapturedRosterMember_RequiresItsNormalVisiblePlayerParty()
    {
        Assert.True(UncapturedRosterMember());
        Assert.False(UncapturedRosterMember(partyVisible: false));
        Assert.False(UncapturedRosterMember(partyLeaderIsHero: false));
        Assert.False(UncapturedRosterMember(partyIsAtSea: true));
    }
#endif

    private static DefenderFixturePartyState CreateState(
        string currentSettlementId = null,
        string lastVisitedSettlementId = null,
        float positionX = 1f,
        float positionY = 3f,
        bool positionIsOnLand = true,
        float bearingY = 2f,
        bool hasMapEvent = false,
        bool hasBesiegerCamp = false,
        bool isAtSea = false,
        bool isHolding = true,
        bool isTransitionInProgress = false,
        bool hasArmy = false,
        bool hasAttachedTo = false,
        bool hasAttachedParties = false) =>
        new DefenderFixturePartyState(
            partyId: "party-1",
            isActive: true,
            isAtSea: isAtSea,
            isHolding: isHolding,
            currentSettlementId: currentSettlementId,
            lastVisitedSettlementId: lastVisitedSettlementId,
            positionX: positionX,
            positionY: positionY,
            positionIsOnLand: positionIsOnLand,
            bearingX: 0f,
            bearingY: bearingY,
            hasMapEvent: hasMapEvent,
            hasBesiegerCamp: hasBesiegerCamp,
            isTransitionInProgress: isTransitionInProgress,
            hasArmy: hasArmy,
            hasAttachedTo: hasAttachedTo,
            hasAttachedParties: hasAttachedParties);

#if DEBUG
    private static bool CaptiveRosterBaseline(
        bool heroIsPrisoner = true,
        bool heroHasCaptor = true,
        bool captorPartyIsActive = true,
        bool heroHasParty = false,
        bool heroStateIsPrisoner = true,
        int captorHeroCount = 1,
        int captorHeroWoundedNumber = 0,
        int captorHeroXp = 0,
        bool partyActive = false,
        bool partyVisible = false,
        bool partyHasVisual = false,
        bool partyHasLeader = false,
        int partyHeroMemberCount = 0,
        int partyMemberCount = 0,
        int partyPrisonerCount = 0,
        bool partyHasCurrentSettlement = false,
        bool partyHasMapEvent = false,
        bool partyHasBesiegerCamp = false,
        bool partyIsTransitioning = false,
        bool partyHasArmy = false,
        bool partyHasAttachedTo = false,
        bool partyHasAttachedParties = false,
        bool partyIsAtSea = false,
        bool heroStaysInSettlement = false) =>
        DefenderRosterFixtureContract.IsCaptiveBaselineRestorable(
            heroIsPrisoner,
            heroHasCaptor,
            captorPartyIsActive,
            heroHasParty,
            heroStateIsPrisoner,
            captorHeroCount,
            captorHeroWoundedNumber,
            captorHeroXp,
            partyActive,
            partyVisible,
            partyHasVisual,
            partyHasLeader,
            partyHeroMemberCount,
            partyMemberCount,
            partyPrisonerCount,
            partyHasCurrentSettlement,
            partyHasMapEvent,
            partyHasBesiegerCamp,
            partyIsTransitioning,
            partyHasArmy,
            partyHasAttachedTo,
            partyHasAttachedParties,
            partyIsAtSea,
            heroStaysInSettlement);

    private static bool UncapturedRosterMember(
        bool heroIsPrisoner = false,
        bool heroHasCaptor = false,
        bool heroBelongsToPlayerParty = true,
        bool heroStateIsActive = true,
        bool partyActive = true,
        bool partyVisible = true,
        bool partyHasVisual = true,
        bool partyLeaderIsHero = true,
        bool partyHasMapEvent = false,
        bool partyHasBesiegerCamp = false,
        bool partyIsTransitioning = false,
        bool partyHasArmy = false,
        bool partyHasAttachedTo = false,
        bool partyHasAttachedParties = false,
        bool partyIsAtSea = false) =>
        DefenderRosterFixtureContract.IsUncapturedPlayerReady(
            heroIsPrisoner,
            heroHasCaptor,
            heroBelongsToPlayerParty,
            heroStateIsActive,
            partyActive,
            partyVisible,
            partyHasVisual,
            partyLeaderIsHero,
            partyHasMapEvent,
            partyHasBesiegerCamp,
            partyIsTransitioning,
            partyHasArmy,
            partyHasAttachedTo,
            partyHasAttachedParties,
            partyIsAtSea);

    private static bool ReleasedRosterReadiness(
        bool heroIsPrisoner = false,
        bool heroHasCaptor = false,
        bool heroBelongsToPlayerParty = true,
        bool heroStateIsActive = true,
        bool partyActive = true,
        bool partyVisible = true,
        bool partyHasVisual = true,
        bool partyLeaderIsHero = true,
        int partyHeroMemberCount = 1) =>
        DefenderRosterFixtureContract.IsReleasedForReadiness(
            heroIsPrisoner,
            heroHasCaptor,
            heroBelongsToPlayerParty,
            heroStateIsActive,
            partyActive,
            partyVisible,
            partyHasVisual,
            partyLeaderIsHero,
            partyHeroMemberCount);

    private static bool ReleasedRosterRestore(
        bool heroIsPrisoner = false,
        bool heroHasCaptor = false,
        bool heroBelongsToPlayerParty = true,
        bool heroStateIsActive = true,
        bool partyActive = true,
        bool partyVisible = true,
        bool partyHasVisual = true,
        bool partyLeaderIsHero = true,
        bool partyHasCurrentSettlement = false,
        bool partyHasMapEvent = false,
        bool partyHasBesiegerCamp = false,
        bool partyIsTransitioning = false,
        bool partyHasArmy = false,
        bool partyHasAttachedTo = false,
        bool partyHasAttachedParties = false,
        bool partyIsAtSea = false,
        int partyHeroMemberCount = 1,
        int partyMemberCount = 1,
        int partyPrisonerCount = 0,
        bool captorPartyIsActive = true,
        int captorHeroPrisonerCount = 0) =>
        DefenderRosterFixtureContract.IsReleasedForRestoration(
            heroIsPrisoner,
            heroHasCaptor,
            heroBelongsToPlayerParty,
            heroStateIsActive,
            partyActive,
            partyVisible,
            partyHasVisual,
            partyLeaderIsHero,
            partyHasCurrentSettlement,
            partyHasMapEvent,
            partyHasBesiegerCamp,
            partyIsTransitioning,
            partyHasArmy,
            partyHasAttachedTo,
            partyHasAttachedParties,
            partyIsAtSea,
            partyHeroMemberCount,
            partyMemberCount,
            partyPrisonerCount,
            captorPartyIsActive,
            captorHeroPrisonerCount);
#endif
}
