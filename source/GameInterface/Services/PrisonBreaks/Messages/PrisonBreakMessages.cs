using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PrisonBreaks.Messages;

public enum PrisonBreakOutcome
{
    Succeeded,
    SucceededJoinedParty,
    PrisonerLeftBehind,
    PlayerCaptured,
    Cancelled,
}

public enum PrisonBreakStartResult
{
    Approved,
    OnCooldown,
    PrisonerUnavailable,
    NotEnoughGold,
}

public readonly struct PrisonBreakStartRequested : IEvent
{
    public readonly Hero PlayerHero;
    public readonly Settlement Settlement;
    public readonly Hero Prisoner;

    public PrisonBreakStartRequested(Hero playerHero, Settlement settlement, Hero prisoner)
    {
        PlayerHero = playerHero;
        Settlement = settlement;
        Prisoner = prisoner;
    }
}

public readonly struct PrisonBreakEnded : IEvent
{
    public readonly PrisonBreakOutcome Outcome;

    public PrisonBreakEnded(PrisonBreakOutcome outcome)
    {
        Outcome = outcome;
    }
}

public readonly struct PrisonBreakCooldownSet : IEvent
{
    public readonly Settlement Settlement;
    public readonly CampaignTime Until;

    public PrisonBreakCooldownSet(Settlement settlement, CampaignTime until)
    {
        Settlement = settlement;
        Until = until;
    }
}

public readonly struct PrisonBreakMissionEnded : IEvent
{
    public readonly Settlement Settlement;

    public PrisonBreakMissionEnded(Settlement settlement)
    {
        Settlement = settlement;
    }
}
