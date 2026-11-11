using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using Romance = TaleWorlds.CampaignSystem.Romance;

namespace GameInterface.Services.Heroes.RomanceFlow;

/// <summary>
/// v1.5.4 lets a failed courtship start again after one season. Vanilla keeps the failed persuasion
/// attempts in <see cref="RomanceCampaignBehavior"/>, keyed by the courted hero, and saves them. The
/// persuasion runs on the client, so the server records the failure itself when it applies a failed
/// level, and sends the failure time to the clients with the romance states.
/// </summary>
internal static class RomanceRetryCooldown
{
    public static bool IsFailedCourtship(Romance.RomanceLevelEnum level) =>
        level == Romance.RomanceLevelEnum.FailedInCompatibility ||
        level == Romance.RomanceLevelEnum.FailedInPracticalities;

    private static RomanceCampaignBehavior Behavior =>
        Campaign.Current?.GetCampaignBehavior<RomanceCampaignBehavior>();

    public static void RecordFailure(Hero courtedHero) => AddFailure(courtedHero, CampaignTime.Now);

    /// <summary>
    /// The same test as vanilla EvaluateCourtshipProgress: an attempt exists and no attempt failed
    /// within one season.
    /// </summary>
    public static bool HasRetryCooldownExpired(Hero courtedHero)
    {
        var behavior = Behavior;
        if (behavior?._previousRomancePersuasionAttempts == null) return false;

        var hasAttempt = false;
        foreach (var attempt in behavior._previousRomancePersuasionAttempts)
        {
            if (attempt.PersuadedHero != courtedHero) continue;
            hasAttempt = true;
            if (!attempt.IsSuccesful() &&
                behavior.IsPersuiasionAttemptInCooldown(attempt.GameTime)) return false;
        }

        return hasAttempt;
    }

    /// <summary>Vanilla clears the attempts for the courted hero when the courtship starts again.</summary>
    public static void Clear(Hero courtedHero) => Behavior?.ClearRomancePersuasionAttemptsFor(courtedHero);

    /// <summary>The ticks of the latest failed attempt for the courted hero, or 0 if there is none.</summary>
    public static long LatestFailureTicks(Hero courtedHero)
    {
        var attempts = Behavior?._previousRomancePersuasionAttempts;
        if (attempts == null) return 0;

        long latest = 0;
        foreach (var attempt in attempts)
        {
            if (attempt.PersuadedHero == courtedHero && !attempt.IsSuccesful() &&
                attempt.GameTime.NumTicks > latest)
                latest = attempt.GameTime.NumTicks;
        }

        return latest;
    }

    /// <summary>Replaces the attempts for the courted hero with the server's latest failure.</summary>
    public static void ReplaceWithFailure(Hero courtedHero, long failureTicks)
    {
        Clear(courtedHero);
        if (failureTicks > 0) AddFailure(courtedHero, new CampaignTime(failureTicks));
    }

    // Vanilla reads the option arguments only for a successful attempt, so a failure needs none.
    private static void AddFailure(Hero courtedHero, CampaignTime time) =>
        Behavior?._previousRomancePersuasionAttempts?.Add(
            new PersuasionAttempt(courtedHero, time, null, PersuasionOptionResult.Failure, 0));
}
