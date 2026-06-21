namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

public enum PlayerPartyInteractionPhase
{
    None,
    InitialOptions,
    OfferServices,
    WaitingForProposal,
    WaitingForResponse,
    ProposalPending,
    TradeActive
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
    Leave
}

public enum PlayerPartyInteractionProposal
{
    None,
    Trade,
    JoinClan,
    Vassal
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
    Disconnected
}

public enum PlayerPartyInteractionDeniedReason
{
    None,
    Busy,
    Hostile
}
