namespace GameInterface.Services.Villages.Data;

public enum VillageHostileActionDeniedReason
{
    Invalid,
    InvalidRequester,
    NonVillageSettlement,
    OwnFaction,
    AlreadyInMapEvent,
    InvalidVillageState,
    HearthTooLow,
    Cooldown,
    NotApproved
}
