#if DEBUG
using Coop.Core.Common.Commands;
using Xunit;

namespace Coop.Tests.Debug;

public class JoinDebugCommandsTests
{
    [Fact]
    public void Classify_ReportsEligibleOnlyForAnActiveSynchronizedNonCaptiveParty()
    {
        string classification = PlayerPartyReadinessContract.Classify(
            playerRegistered: true,
            connected: true,
            currentPeerBinding: true,
            completedSynchronization: true,
            partyResolved: true,
            partyActive: true,
            heroResolved: true,
            heroCaptive: false);

        Assert.Equal(PlayerPartyReadinessContract.Eligible, classification);
        Assert.Null(PlayerPartyReadinessContract.GetReason(classification));
    }

    [Fact]
    public void Classify_RejectsCaptivePlayersAsAnInvalidFixtureRoster()
    {
        string classification = PlayerPartyReadinessContract.Classify(
            playerRegistered: true,
            connected: true,
            currentPeerBinding: true,
            completedSynchronization: true,
            partyResolved: true,
            partyActive: false,
            heroResolved: true,
            heroCaptive: true);

        Assert.Equal(PlayerPartyReadinessContract.InvalidFixtureRosterCaptive, classification);
    }

    [Fact]
    public void Classify_DistinguishesAnInactiveCurrentSynchronizedNonCaptiveParty()
    {
        string classification = PlayerPartyReadinessContract.Classify(
            playerRegistered: true,
            connected: true,
            currentPeerBinding: true,
            completedSynchronization: true,
            partyResolved: true,
            partyActive: false,
            heroResolved: true,
            heroCaptive: false);

        Assert.Equal(
            PlayerPartyReadinessContract.CurrentSynchronizedNonCaptiveInactive,
            classification);
    }

    [Theory]
    [InlineData(false, true, true, true, true, true)]
    [InlineData(true, false, true, true, true, true)]
    [InlineData(true, true, false, true, true, true)]
    [InlineData(true, true, true, false, true, true)]
    [InlineData(true, true, true, true, false, true)]
    [InlineData(true, true, true, true, true, false)]
    public void Classify_RejectsUnavailablePlayersWhenARequiredBoundaryIsMissing(
        bool playerRegistered,
        bool connected,
        bool currentPeerBinding,
        bool completedSynchronization,
        bool partyResolved,
        bool heroResolved)
    {
        string classification = PlayerPartyReadinessContract.Classify(
            playerRegistered,
            connected,
            currentPeerBinding,
            completedSynchronization,
            partyResolved,
            partyActive: false,
            heroResolved,
            heroCaptive: false);

        Assert.Equal(PlayerPartyReadinessContract.InvalidFixtureRosterUnavailable, classification);
    }
}
#endif
