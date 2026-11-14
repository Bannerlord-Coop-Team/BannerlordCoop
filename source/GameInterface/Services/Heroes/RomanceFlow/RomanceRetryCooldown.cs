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
    void RecordFailure(Hero playerHero, Hero courtedHero);

    /// <summary>Forgets the failure when the courtship starts again, as vanilla does.</summary>
    void Clear(Hero playerHero, Hero courtedHero);

    /// <summary>
    /// True when no failure of this player with this hero is less than one season old. A failed courtship
    /// with no record, from a save written before the history existed, can be retried.
    /// </summary>
    bool HasRetryCooldownExpired(Hero playerHero, Hero courtedHero);

    /// <summary>
    /// The ticks of this player's failure with this hero, for a couple at a failed level. Without a record it
    /// is the first tick of the campaign, so the client's retry dialogue allows what the server allows.
    /// </summary>
    long FailureTicks(Hero playerHero, Hero courtedHero);

    /// <summary>On a client, replaces vanilla's attempts for the courted hero with the server's failure.</summary>
    void RestoreLocalFailure(Hero courtedHero, long failureTicks);
}

internal class RomanceRetryCooldown : IRomanceRetryCooldown
{
    private readonly IRomanceRetryHistory history;

    public RomanceRetryCooldown(IRomanceRetryHistory history)
    {
        this.history = history;
    }

    public void RecordFailure(Hero playerHero, Hero courtedHero) =>
        history.RecordFailure(playerHero, courtedHero, CampaignTime.Now);

    public void Clear(Hero playerHero, Hero courtedHero) => history.Clear(playerHero, courtedHero);

    // The same test as vanilla's IsPersuiasionAttemptInCooldown: less than one season since the failure.
    public bool HasRetryCooldownExpired(Hero playerHero, Hero courtedHero) =>
        !history.TryGetFailure(playerHero, courtedHero, out var failedAt) || failedAt.ElapsedSeasonsUntilNow >= 1f;

    public long FailureTicks(Hero playerHero, Hero courtedHero) =>
        history.TryGetFailure(playerHero, courtedHero, out var failedAt) ? failedAt.NumTicks : 1;

    public void RestoreLocalFailure(Hero courtedHero, long failureTicks)
    {
        var behavior = Campaign.Current?.GetCampaignBehavior<RomanceCampaignBehavior>();
        if (behavior?._previousRomancePersuasionAttempts == null) return;

        behavior.ClearRomancePersuasionAttemptsFor(courtedHero);
        // Vanilla reads the option arguments only for a successful attempt, so a failure needs none.
        if (failureTicks > 0)
            behavior._previousRomancePersuasionAttempts.Add(new PersuasionAttempt(
                courtedHero, new CampaignTime(failureTicks), null, PersuasionOptionResult.Failure, 0));
    }
}
