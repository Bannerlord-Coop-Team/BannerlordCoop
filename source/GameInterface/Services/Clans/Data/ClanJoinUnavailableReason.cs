namespace GameInterface.Services.Clans.Data;

public enum ClanJoinUnavailableReason
{
    None,
    MissingClan,
    SameClan,
    OtherPlayersInClan,
    TargetIsNotClanLeader,
    RulesKingdom,
    Mercenary,
    Vassal,
    OwnsFiefs,
    IncompatibleWars
}
