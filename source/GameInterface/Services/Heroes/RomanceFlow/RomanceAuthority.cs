using GameInterface.Services.Heroes.Extensions;
using System.Linq;
using TaleWorlds.CampaignSystem;
using Romance = TaleWorlds.CampaignSystem.Romance;

namespace GameInterface.Services.Heroes.RomanceFlow;

internal interface IRomanceAuthority : IGameAbstraction
{
    bool TryValidateStateChange(
        Hero playerHero,
        Hero targetHero,
        Romance.RomanceLevelEnum requestedLevel,
        out string reason);

    bool TryValidateMarriage(Hero playerHero, Hero targetHero, out string reason);
}

internal class RomanceAuthority : IRomanceAuthority
{
    public bool TryValidateStateChange(
        Hero playerHero,
        Hero targetHero,
        Romance.RomanceLevelEnum requestedLevel,
        out string reason)
    {
        if (!TryValidatePair(playerHero, targetHero, out reason)) return false;

        var currentLevel = Romance.GetRomanticLevel(playerHero, targetHero);
        if (!RomanceTransitionRules.IsAllowed(currentLevel, requestedLevel))
        {
            reason = $"Cannot change romance from {currentLevel} to {requestedLevel}.";
            return false;
        }

        if (RequiresMarriageEligibility(requestedLevel) &&
            !Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(playerHero, targetHero))
        {
            reason = "That pair is not currently eligible for marriage.";
            return false;
        }

        if (IsActiveCourtship(requestedLevel) && HasOtherActiveCourtship(targetHero, playerHero))
        {
            reason = "That hero is already being courted.";
            return false;
        }

        reason = null;
        return true;
    }

    public bool TryValidateMarriage(Hero playerHero, Hero targetHero, out string reason)
    {
        if (!TryValidatePair(playerHero, targetHero, out reason)) return false;

        if (Romance.GetRomanticLevel(playerHero, targetHero) != Romance.RomanceLevelEnum.CoupleAgreedOnMarriage)
        {
            reason = "The couple has not agreed on marriage.";
            return false;
        }

        if (!Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(playerHero, targetHero))
        {
            reason = "That pair is not currently eligible for marriage.";
            return false;
        }

        reason = null;
        return true;
    }

    private static bool TryValidatePair(Hero playerHero, Hero targetHero, out string reason)
    {
        if (playerHero == null || targetHero == null)
        {
            reason = "Both heroes must exist.";
            return false;
        }

        if (playerHero == targetHero)
        {
            reason = "A hero cannot court themselves.";
            return false;
        }

        if (!playerHero.IsAlive || !targetHero.IsAlive)
        {
            reason = "Both heroes must be alive.";
            return false;
        }

        if (targetHero.IsPlayerHero())
        {
            reason = "Player-to-player romance is not supported.";
            return false;
        }

        if (playerHero.Spouse != null || targetHero.Spouse != null)
        {
            reason = "Both heroes must be unmarried.";
            return false;
        }

        reason = null;
        return true;
    }

    private static bool RequiresMarriageEligibility(Romance.RomanceLevelEnum level)
        => level != Romance.RomanceLevelEnum.FailedInCompatibility &&
           level != Romance.RomanceLevelEnum.FailedInPracticalities &&
           level != Romance.RomanceLevelEnum.Rejection;

    private static bool IsActiveCourtship(Romance.RomanceLevelEnum level)
        => level >= Romance.RomanceLevelEnum.MatchMadeByFamily &&
           level < Romance.RomanceLevelEnum.Marriage;

    private static bool HasOtherActiveCourtship(Hero targetHero, Hero playerHero)
        => Romance.RomanticStateList?.Any(state =>
            state != null &&
            IsActiveCourtship(state.Level) &&
            (state.Person1 == targetHero || state.Person2 == targetHero) &&
            state.Person1 != playerHero &&
            state.Person2 != playerHero) == true;
}

internal static class RomanceTransitionRules
{
    public static bool IsAllowed(
        Romance.RomanceLevelEnum currentLevel,
        Romance.RomanceLevelEnum requestedLevel)
        => requestedLevel switch
        {
            Romance.RomanceLevelEnum.MatchMadeByFamily =>
                currentLevel == Romance.RomanceLevelEnum.Untested,
            Romance.RomanceLevelEnum.CourtshipStarted =>
                currentLevel == Romance.RomanceLevelEnum.Untested ||
                currentLevel == Romance.RomanceLevelEnum.MatchMadeByFamily,
            Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible =>
                currentLevel == Romance.RomanceLevelEnum.CourtshipStarted,
            Romance.RomanceLevelEnum.CoupleAgreedOnMarriage =>
                currentLevel == Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible,
            Romance.RomanceLevelEnum.FailedInCompatibility =>
                currentLevel == Romance.RomanceLevelEnum.MatchMadeByFamily ||
                currentLevel == Romance.RomanceLevelEnum.CourtshipStarted ||
                currentLevel == Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible,
            Romance.RomanceLevelEnum.FailedInPracticalities =>
                currentLevel == Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible,
            Romance.RomanceLevelEnum.Rejection =>
                currentLevel == Romance.RomanceLevelEnum.MatchMadeByFamily ||
                currentLevel == Romance.RomanceLevelEnum.CourtshipStarted ||
                currentLevel == Romance.RomanceLevelEnum.CoupleDecidedThatTheyAreCompatible,
            _ => false,
        };
}
