namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

public enum PlayerPartyInteractionPhase
{
    None,
    InitialOptions,
    OfferServices,
    WaitingForProposal,
    WaitingForResponse,
    ProposalPending,
    TradeActive,
    HostileDemandConfirm,
    HostileDemandPending,
    MarriageOptions
}

public enum PlayerPartyInteractionOption
{
    None,
    TradeProposal,
    OfferServices,
    JoinClan,
    Vassal,
    AcceptProposal,
    DeclineProposal,
    Leave,
    HostileDemand,
    ConfirmHostileDemand,
    CancelHostileDemand,
    RefuseHostileDemand,
    YieldHostileDemand,
    LeaveClan,
    RemoveFromClan,
    ProposeMarriage,
    PatrilinealMarriage,
    MatrilinealMarriage,
    CancelMarriage
}

public enum PlayerPartyInteractionVassalUnavailableReason
{
    None,
    TargetIsNotKingdomLeader,
    InitiatorHasNoClan,
    InitiatorIsInKingdom,
    InitiatorClanTierTooLow
}

public enum PlayerPartyInteractionProposal
{
    None,
    Trade,
    JoinClan,
    Vassal,
    HostileDemand,
    PatrilinealMarriage,
    MatrilinealMarriage
}

public enum PlayerPartyInteractionOutcomeType
{
    None,
    Left,
    TradeAccepted,
    TradeDeclined,
    ClanJoinAccepted,
    ClanJoinDeclined,
    VassalAccepted,
    VassalDeclined,
    Rejected,
    Disconnected,
    HostileDemandAccepted,
    HostileDemandYielded,
    ClanLeft,
    ClanMemberRemoved,
    MarriageAccepted,
    MarriageDeclined
}

public enum PlayerPartyInteractionDeniedReason
{
    None,
    Busy,
    Hostile
}
