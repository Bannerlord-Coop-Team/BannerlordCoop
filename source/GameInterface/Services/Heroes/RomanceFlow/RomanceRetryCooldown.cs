using GameInterface.Services.Heroes.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using Romance = TaleWorlds.CampaignSystem.Romance;

namespace GameInterface.Services.Heroes.RomanceFlow;

/// <summary>
/// v1.5.4 lets a failed courtship start again after one season (#4588). Vanilla keeps the failed persuasion
/// attempts in <see cref="RomanceCampaignBehavior"/>, keyed by the courted hero only. The persuasion runs on
/// the client, so the server keeps each player's failure in <see cref="IRomanceRetryHistory"/>, and a client
/// puts its own failures into vanilla's list, where the retry dialogue reads them.
/// </summary>
internal interface IRomanceRetryCooldown
{
    /// <summary>
    /// Records the failure on a move to a failed level, and forgets it when the failed courtship starts again, as
    /// vanilla does.
    /// </summary>
    void OnStateChange(
        Hero playerHero,
        Hero courtedHero,
        Romance.RomanceLevelEnum currentLevel,
        Romance.RomanceLevelEnum requestedLevel);

    /// <summary>
    /// True when no failure of this player with this hero is less than one season old. A failed courtship
    /// with no record, from a save written before the history existed, can be retried.
    /// </summary>
    bool HasRetryCooldownExpired(Hero playerHero, Hero courtedHero);

    /// <summary>
    /// The ticks of the player's failure in this couple, or 0 when it is not a failed courtship of a player. Without
    /// a record it is the first tick of the campaign, so the client's retry dialogue allows what the server allows.
    /// </summary>
    long FailureTicks(Hero person1, Hero person2, Romance.RomanceLevelEnum level);

    /// <summary>
    /// On a client, replaces vanilla's attempts for the hero the main hero courts with the server's failure.
    /// </summary>
    void RestoreLocalFailure(Hero person1, Hero person2, Romance.RomanceLevelEnum level, long failureTicks);
}

internal class RomanceRetryCooldown : IRomanceRetryCooldown
{
    private readonly IRomanceRetryHistory history;

    public RomanceRetryCooldown(IRomanceRetryHistory history)
    {
        this.history = history;
    }

    public void OnStateChange(
        Hero playerHero,
        Hero courtedHero,
        Romance.RomanceLevelEnum currentLevel,
        Romance.RomanceLevelEnum requestedLevel)
    {
        if (RomanceTransitionRules.IsFailedCourtship(currentLevel) && requestedLevel == Romance.RomanceLevelEnum.CourtshipStarted)
            history.Clear(playerHero, courtedHero);
        else if (RomanceTransitionRules.IsFailedCourtship(requestedLevel))
            history.RecordFailure(playerHero, courtedHero, CampaignTime.Now);
    }

    // The same test as vanilla's IsPersuiasionAttemptInCooldown: less than one season since the failure.
    public bool HasRetryCooldownExpired(Hero playerHero, Hero courtedHero) =>
        !history.TryGetFailure(playerHero, courtedHero, out var failedAt) || failedAt.ElapsedSeasonsUntilNow >= 1f;

    public long FailureTicks(Hero person1, Hero person2, Romance.RomanceLevelEnum level)
    {
        if (!RomanceTransitionRules.IsFailedCourtship(level)) return 0;

        // A courtship is between a player's hero and a hero no player controls
        Hero playerHero;
        if (person1.IsPlayerHero()) playerHero = person1;
        else if (person2.IsPlayerHero()) playerHero = person2;
        else return 0;

        Hero courtedHero = playerHero == person1 ? person2 : person1;
        return history.TryGetFailure(playerHero, courtedHero, out var failedAt) ? failedAt.NumTicks : 1;
    }

    public void RestoreLocalFailure(Hero person1, Hero person2, Romance.RomanceLevelEnum level, long failureTicks)
    {
        // Vanilla's attempt list has no player, so a client keeps only its own hero's failures
        var mainHero = Hero.MainHero;
        if (mainHero == null || !RomanceTransitionRules.IsFailedCourtship(level)) return;
        if (person1 != mainHero && person2 != mainHero) return;

        var behavior = Campaign.Current?.GetCampaignBehavior<RomanceCampaignBehavior>();
        if (behavior?._previousRomancePersuasionAttempts == null) return;

        Hero courtedHero = person1 == mainHero ? person2 : person1;
        behavior.ClearRomancePersuasionAttemptsFor(courtedHero);
        // Vanilla reads the option arguments only for a successful attempt, so a failure needs none.
        if (failureTicks > 0)
            behavior._previousRomancePersuasionAttempts.Add(new PersuasionAttempt(
                courtedHero, new CampaignTime(failureTicks), null, PersuasionOptionResult.Failure, 0));
    }
}
