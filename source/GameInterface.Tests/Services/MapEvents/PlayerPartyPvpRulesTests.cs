using GameInterface.Configuration;
using GameInterface.Services.MapEvents.PlayerPartyInteractions;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class PlayerPartyPvpRulesTests
{
    private readonly PlayerPartyPvpRules rules = new PlayerPartyPvpRules();

    [Theory]
    [InlineData(PlayerPartyInteractionProposal.HostileDemand, true, true, PlayerPartyInteractionOutcomeType.HostileDemandAccepted)]
    [InlineData(PlayerPartyInteractionProposal.HostileDemand, true, false, PlayerPartyInteractionOutcomeType.Left)]
    [InlineData(PlayerPartyInteractionProposal.HostileDemand, false, true, PlayerPartyInteractionOutcomeType.Left)]
    [InlineData(PlayerPartyInteractionProposal.Vassal, true, false, PlayerPartyInteractionOutcomeType.Left)]
    public void GetTimeoutOutcome_StartsTheBattleOnlyForAConfirmedDemandAtWar(
        PlayerPartyInteractionProposal proposal, bool atWar, bool demandConfirmed, PlayerPartyInteractionOutcomeType expectedOutcome)
    {
        var session = CreateSession(proposal);
        session.HostileDemandConfirmed = demandConfirmed;

        Assert.Equal(expectedOutcome, rules.GetTimeoutOutcome(session, atWar));
    }

    [Theory]
    [InlineData(PlayerPartyInteractionProposal.Vassal, 1, 10)]
    [InlineData(PlayerPartyInteractionProposal.Vassal, 10, 10)]
    [InlineData(PlayerPartyInteractionProposal.Vassal, 45, 45)]
    [InlineData(PlayerPartyInteractionProposal.Trade, 30, 0)]
    public void GetIdleSeconds_RaisesAShortResponseTimerToTenSeconds_AndNeverTimesATrade(
        PlayerPartyInteractionProposal proposal, int responseTimerSeconds, int expectedSeconds)
    {
        var session = CreateSession(proposal);

        Assert.Equal(expectedSeconds, rules.GetIdleSeconds(session, Options(blockLeave: false, responseTimerSeconds)));
    }

    [Theory]
    [InlineData(PlayerPartyInteractionProposal.None, true, 30)]
    [InlineData(PlayerPartyInteractionProposal.None, false, 0)]
    [InlineData(PlayerPartyInteractionProposal.HostileDemand, true, 30)]
    [InlineData(PlayerPartyInteractionProposal.Vassal, true, 0)]
    [InlineData(PlayerPartyInteractionProposal.Trade, true, 0)]
    public void GetIdleSeconds_WithOnlyBlockLeave_ReleasesAnIdleAttacker(
        PlayerPartyInteractionProposal proposal, bool leaveHidden, int expectedSeconds)
    {
        var session = CreateSession(proposal);
        session.IsLeaveHidden = leaveHidden;

        Assert.Equal(expectedSeconds, rules.GetIdleSeconds(session, Options(blockLeave: true, responseTimerSeconds: 0)));
    }

    [Theory]
    [InlineData(true, true, PlayerPartyInteractionProposal.None, true, true)]
    [InlineData(false, true, PlayerPartyInteractionProposal.None, true, false)]
    [InlineData(true, false, PlayerPartyInteractionProposal.None, true, false)]
    [InlineData(true, true, PlayerPartyInteractionProposal.Trade, true, false)]
    [InlineData(true, true, PlayerPartyInteractionProposal.None, false, false)]
    public void IsLeaveBlocked_OnlyForAMeetingStartedAtWarThatIsStillAtWar(
        bool blockLeave, bool isHostile, PlayerPartyInteractionProposal proposal, bool atWar, bool expected)
    {
        var session = CreateSession(proposal, isHostile);

        Assert.Equal(expected, rules.IsLeaveBlocked(session, Options(blockLeave, responseTimerSeconds: 0), () => atWar));
    }

    [Theory]
    [InlineData(false, 0, false)]
    [InlineData(true, 0, true)]
    [InlineData(false, 30, true)]
    [InlineData(false, -5, false)]
    public void IsEnabled_WhenBlockLeaveIsOnOrTheTimerIsAboveZero(bool blockLeave, int responseTimerSeconds, bool expected)
    {
        Assert.Equal(expected, rules.IsEnabled(Options(blockLeave, responseTimerSeconds)));
    }

    private static PlayerPartyInteractionSession CreateSession(PlayerPartyInteractionProposal proposal, bool isHostile = true)
        => new PlayerPartyInteractionSession("session", "initiator", "responder", "Initiator", "Responder", null, isHostile)
        {
            Proposal = proposal,
        };

    private static ModOptions Options(bool blockLeave, int responseTimerSeconds)
        => new ModOptions(new ModOptionsData
        {
            PvpBlockLeave = blockLeave,
            PvpResponseTimerSeconds = responseTimerSeconds,
        });
}
