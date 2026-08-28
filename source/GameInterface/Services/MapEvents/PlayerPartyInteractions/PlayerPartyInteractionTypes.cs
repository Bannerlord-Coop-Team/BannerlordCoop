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
    MercenaryConfirm
}

public enum PlayerPartyInteractionOption
{
    None,
    TradeProposal,
    OfferServices,
    JoinClan,
    Vassal,
    Mercenary,
    ConfirmMercenary,
    CancelMercenary,
    AcceptProposal,
    DeclineProposal,
    Leave,
    HostileDemand,
    ConfirmHostileDemand,
    CancelHostileDemand,
    RefuseHostileDemand,
    YieldHostileDemand
}

public enum PlayerPartyInteractionVassalUnavailableReason
{
    None,
    TargetIsNotKingdomLeader,
    InitiatorHasNoClan,
    InitiatorIsInKingdom,
    InitiatorClanTierTooLow
}

public enum PlayerPartyInteractionMercenaryUnavailableReason
{
    None,
    InitiatorHasNoClan,
    InitiatorIsNotClanLeader,
    InitiatorClanTierTooLow,
    AlreadyMercenaryForThisKingdom,
    InitiatorClanHasSettlement,
    NotEnoughRelation,
    ClanIsInKingdom,
    TargetHasNoKingdom,
    IncompatibleWars
}
public enum PlayerPartyInteractionProposal
{
    None,
    Trade,
    JoinClan,
    Vassal,
    Mercenary,
    HostileDemand
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
    MercenaryAccepted,
    MercenaryDeclined,
    Rejected,
    Disconnected,
    HostileDemandAccepted,
    HostileDemandYielded
}

public enum PlayerPartyInteractionDeniedReason
{
    None,
    Busy,
    Hostile
}
