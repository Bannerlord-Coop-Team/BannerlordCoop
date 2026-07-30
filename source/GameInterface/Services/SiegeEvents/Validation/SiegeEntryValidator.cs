using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents.Validation;

public enum SiegeEntryAction
{
    Besiege,
    Join,
}

public enum SiegeEntryDenialReason
{
    None,
    InvalidRequester,
    MissingInteractionGrant,
    InvalidParty,
    ActiveMapEvent,
    ConflictingSettlement,
    ConflictingSiege,
    MovementTargetMismatch,
    TooFar,
    InvalidSettlement,
    InvalidFaction,
    DefenderDisposition,
    StaleSiegeLink,
    ActionFailed,
}

public enum SiegeEntryDisposition
{
    Map,
    Settlement,
    Besieger,
    MapEvent,
}

public readonly struct SiegeEntryCanonicalState
{
    public SiegeEntryDisposition Disposition { get; }
    public Settlement Settlement { get; }

    public SiegeEntryCanonicalState(SiegeEntryDisposition disposition, Settlement settlement)
    {
        Disposition = disposition;
        Settlement = settlement;
    }
}

public readonly struct SiegeEntryValidationResult
{
    public bool IsValid { get; }
    public SiegeEntryDenialReason Reason { get; }
    public SiegeEntryCanonicalState CanonicalState { get; }

    private SiegeEntryValidationResult(
        bool isValid,
        SiegeEntryDenialReason reason,
        SiegeEntryCanonicalState canonicalState)
    {
        IsValid = isValid;
        Reason = reason;
        CanonicalState = canonicalState;
    }

    public static SiegeEntryValidationResult Valid(SiegeEntryCanonicalState canonicalState) =>
        new SiegeEntryValidationResult(true, SiegeEntryDenialReason.None, canonicalState);

    public static SiegeEntryValidationResult Rejected(
        SiegeEntryDenialReason reason,
        SiegeEntryCanonicalState canonicalState) =>
        new SiegeEntryValidationResult(false, reason, canonicalState);
}

public interface ISiegeEntryValidator
{
    SiegeEntryValidationResult ValidateSettlementInteraction(MobileParty party, Settlement settlement);
    SiegeEntryValidationResult ValidateEntry(MobileParty party, Settlement settlement, SiegeEntryAction action);
    SiegeEntryValidationResult ValidateReloadedBesieger(MobileParty party);
    SiegeEntryCanonicalState GetCanonicalState(MobileParty party);
}

internal sealed class SiegeEntryValidator : ISiegeEntryValidator
{
    private const float SettlementInteractionTolerance = 0.5f;
    private const float ReloadedSiegeLinkMaximumDistance = 8f;

    public SiegeEntryValidationResult ValidateSettlementInteraction(MobileParty party, Settlement settlement)
    {
        var canonicalState = GetCanonicalState(party);
        if (party?.IsActive != true || party.AttachedTo != null)
            return SiegeEntryValidationResult.Rejected(SiegeEntryDenialReason.InvalidParty, canonicalState);

        if (settlement?.Party == null)
            return SiegeEntryValidationResult.Rejected(SiegeEntryDenialReason.InvalidSettlement, canonicalState);

        if (party.MapEvent != null)
            return SiegeEntryValidationResult.Rejected(SiegeEntryDenialReason.ActiveMapEvent, canonicalState);

        if (party.BesiegerCamp != null)
            return SiegeEntryValidationResult.Rejected(SiegeEntryDenialReason.ConflictingSiege, canonicalState);

        if (party.CurrentSettlement != null)
        {
            return party.CurrentSettlement == settlement
                ? SiegeEntryValidationResult.Valid(canonicalState)
                : SiegeEntryValidationResult.Rejected(SiegeEntryDenialReason.ConflictingSettlement, canonicalState);
        }

        if (!HasCoherentSettlementTarget(party, settlement))
            return SiegeEntryValidationResult.Rejected(SiegeEntryDenialReason.MovementTargetMismatch, canonicalState);

        if (!IsWithinSettlementInteractionDistance(party, settlement))
            return SiegeEntryValidationResult.Rejected(SiegeEntryDenialReason.TooFar, canonicalState);

        return SiegeEntryValidationResult.Valid(canonicalState);
    }

    public SiegeEntryValidationResult ValidateEntry(
        MobileParty party,
        Settlement settlement,
        SiegeEntryAction action)
    {
        var interactionResult = ValidateSettlementInteraction(party, settlement);
        if (!interactionResult.IsValid)
            return interactionResult;

        if (!settlement.IsFortification)
        {
            return SiegeEntryValidationResult.Rejected(
                SiegeEntryDenialReason.InvalidSettlement,
                GetCanonicalState(party));
        }

        if (IsSettlementDefender(party, settlement))
        {
            return SiegeEntryValidationResult.Rejected(
                SiegeEntryDenialReason.DefenderDisposition,
                GetCanonicalState(party));
        }

        if (party.MapFaction == null ||
            settlement.MapFaction == null ||
            !FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction))
        {
            return SiegeEntryValidationResult.Rejected(
                SiegeEntryDenialReason.InvalidFaction,
                GetCanonicalState(party));
        }

        if (action == SiegeEntryAction.Besiege)
        {
            if (settlement.SiegeEvent != null || party.Party.NumberOfHealthyMembers <= 0)
            {
                return SiegeEntryValidationResult.Rejected(
                    SiegeEntryDenialReason.InvalidSettlement,
                    GetCanonicalState(party));
            }

            return SiegeEntryValidationResult.Valid(GetCanonicalState(party));
        }

        if (settlement.SiegeEvent == null ||
            !settlement.SiegeEvent.CanPartyJoinSide(party.Party, BattleSideEnum.Attacker))
        {
            return SiegeEntryValidationResult.Rejected(
                SiegeEntryDenialReason.InvalidFaction,
                GetCanonicalState(party));
        }

        return SiegeEntryValidationResult.Valid(GetCanonicalState(party));
    }

    public SiegeEntryValidationResult ValidateReloadedBesieger(MobileParty party)
    {
        var canonicalState = GetCanonicalState(party);
        var camp = party?.BesiegerCamp;
        if (camp == null)
            return SiegeEntryValidationResult.Valid(canonicalState);

        var siegeEvent = camp.SiegeEvent;
        var settlement = siegeEvent?.BesiegedSettlement;
        bool validMapEvent = HasCoherentReloadedMapEvent(party, settlement);

        bool isValid = party.CurrentSettlement == null &&
            siegeEvent != null &&
            settlement != null &&
            settlement.SiegeEvent == siegeEvent &&
            siegeEvent.BesiegerCamp == camp &&
            camp.HasInvolvedPartyForEventType(party.Party) &&
            validMapEvent &&
            !IsSettlementDefender(party, settlement) &&
            party.MapFaction != null &&
            settlement.MapFaction != null &&
            FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction) &&
            IsWithinReloadedSiegeDistance(party, settlement);

        return isValid
            ? SiegeEntryValidationResult.Valid(canonicalState)
            : SiegeEntryValidationResult.Rejected(
                SiegeEntryDenialReason.StaleSiegeLink,
                new SiegeEntryCanonicalState(SiegeEntryDisposition.Map, null));
    }

    public SiegeEntryCanonicalState GetCanonicalState(MobileParty party)
    {
        var besiegedSettlement = party?.BesiegerCamp?.SiegeEvent?.BesiegedSettlement;
        if (besiegedSettlement != null &&
            (party.MapEvent == null || party.MapEvent.MapEventSettlement == besiegedSettlement))
        {
            return new SiegeEntryCanonicalState(
                SiegeEntryDisposition.Besieger,
                besiegedSettlement);
        }

        if (party?.CurrentSettlement != null)
        {
            return new SiegeEntryCanonicalState(
                SiegeEntryDisposition.Settlement,
                party.CurrentSettlement);
        }

        if (party?.MapEvent != null)
        {
            return new SiegeEntryCanonicalState(
                SiegeEntryDisposition.MapEvent,
                party.MapEvent.MapEventSettlement);
        }

        return new SiegeEntryCanonicalState(SiegeEntryDisposition.Map, null);
    }

    private static bool HasCoherentSettlementTarget(MobileParty party, Settlement settlement)
    {
        if (party.TargetSettlement != null && party.TargetSettlement != settlement)
            return false;

        if (party.ShortTermTargetSettlement != null && party.ShortTermTargetSettlement != settlement)
            return false;

        return (party.TargetSettlement == settlement || party.ShortTermTargetSettlement == settlement) &&
            party.IsTargetingPort == party.IsCurrentlyAtSea;
    }

    private static bool IsWithinSettlementInteractionDistance(MobileParty party, Settlement settlement)
    {
        var encounterModel = Campaign.Current.Models.EncounterModel;
        float maximumDistance;
        if (party.IsTargetingPort && settlement.SiegeEvent?.IsBlockadeActive == true)
        {
            maximumDistance = encounterModel.NeededMaximumDistanceForEncounteringBlockade;
        }
        else
        {
            maximumDistance = settlement.IsVillage
                ? encounterModel.NeededMaximumDistanceForEncounteringVillage
                : encounterModel.NeededMaximumDistanceForEncounteringTown;
        }

        var targetPosition = party.IsTargetingPort
            ? settlement.PortPosition
            : settlement.GatePosition;

        return party.Position.Distance(targetPosition) <= maximumDistance + SettlementInteractionTolerance;
    }

    private static bool IsWithinReloadedSiegeDistance(MobileParty party, Settlement settlement)
    {
        var targetPosition = party.IsCurrentlyAtSea && settlement.HasPort
            ? settlement.PortPosition
            : settlement.GatePosition;

        return party.Position.Distance(targetPosition) <= ReloadedSiegeLinkMaximumDistance;
    }

    private static bool IsSettlementDefender(MobileParty party, Settlement settlement)
    {
        if (party == null || settlement == null)
            return false;

        return (party.ActualClan != null && settlement.OwnerClan == party.ActualClan) ||
            (party.MapFaction != null && settlement.MapFaction == party.MapFaction);
    }

    private static bool HasCoherentReloadedMapEvent(
        MobileParty party,
        Settlement settlement)
    {
        var mapEvent = party.MapEvent;
        if (mapEvent == null)
            return true;

        var sides = mapEvent._sides;
        if (sides == null ||
            sides.Length <= (int)BattleSideEnum.Attacker)
        {
            return false;
        }

        var attackerSide = sides[(int)BattleSideEnum.Attacker];
        var defenderSide = sides[(int)BattleSideEnum.Defender];
        if (mapEvent.IsFinalized ||
            mapEvent.MapEventSettlement != settlement ||
            !IsSiegeRelatedMapEvent(mapEvent) ||
            attackerSide?.LeaderParty == null ||
            defenderSide?.LeaderParty == null)
        {
            return false;
        }

        var partySide = party.Party?.MapEventSide;
        return ReferenceEquals(partySide, attackerSide) ||
            ReferenceEquals(partySide, defenderSide);
    }

    private static bool IsSiegeRelatedMapEvent(MapEvent mapEvent) =>
        mapEvent.IsSiegeAssault ||
        mapEvent.IsSallyOut ||
        mapEvent.IsSiegeOutside ||
        mapEvent.IsBlockade ||
        mapEvent.IsBlockadeSallyOut ||
        mapEvent.IsSiegeAmbush;
}
