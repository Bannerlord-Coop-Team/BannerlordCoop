using GameInterface.Services.Barters.Messages;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Barters.Handlers;

internal sealed partial class LordBarterHandler
{
    // defection_successful_on_consequence hardcodes PersuasionDifficulty.Medium, and every
    // PersuasionOptionArgs built by GetPersuasionTasksForDefection passes DefaultSkills.Charm.
    // Neither is taken from the client.
    private const PersuasionDifficulty DefectionPersuasionDifficulty = PersuasionDifficulty.Medium;

    /// <summary>
    /// Records why a barter was refused on value, with the inputs most likely to differ from the
    /// client's own evaluation.
    /// </summary>
    /// <remarks>
    /// Client and server run the same acceptance test, so a disagreement is always an input
    /// disagreement. The kingdom roster and fief caches are the usual culprits - the repo already
    /// notes they are not reliably intercepted - and they feed a quadratic term, so small roster
    /// drift is worth a great deal of gold. Logging the counts here makes a live divergence a diff
    /// rather than a guess.
    /// </remarks>
    private void LogOfferValueBreakdown(
        Hero playerHero,
        Hero targetHero,
        Kingdom targetKingdom,
        BarterData barter,
        float offerValue)
    {
        try
        {
            var breakdown = string.Join(", ", barter.GetOfferedBarterables()
                .Select(b => $"{b.GetType().Name}x{b.CurrentAmount}={b.GetValueForFaction(targetHero.Clan)}"));

            // The requesting player, NOT Hero.MainHero / Clan.PlayerClan: this runs on the server,
            // where those are null on a dedicated host and are the HOST's own hero and clan on a
            // listen host - never the player whose barter was refused, which is the one that matters.
            Logger.Warning(
                "Lord barter refused on value. offerValue={OfferValue} threshold=-0.01 offered=[{Breakdown}] " +
                "playerHero={PlayerHero} playerClan={PlayerClan} targetClan={TargetClan} " +
                "targetKingdom={Kingdom} kingdomClans={ClanCount} kingdomFiefs={FiefCount}",
                offerValue,
                breakdown,
                playerHero?.StringId,
                playerHero?.Clan?.StringId,
                targetHero.Clan?.StringId,
                targetKingdom?.StringId,
                targetKingdom?.Clans?.Count,
                targetKingdom?.Fiefs?.Count);
        }
        catch (Exception exception)
        {
            // Never let diagnostics break the reject path.
            Logger.Warning(exception, "Could not build the lord barter value breakdown");
        }
    }

    /// <summary>
    /// Server-side replay of the XP half of vanilla's
    /// <c>LordDefectionCampaignBehavior.defection_successful_on_consequence</c>.
    /// </summary>
    /// <remarks>
    /// A client winning the persuasion used to gain nothing: the XP writes are blocked client-side
    /// and the server never runs the dialogue. The client cannot be avoided as the source of the
    /// per-attempt outcomes - the mini-game and its rolls live in the client's ConversationManager,
    /// and vanilla's option table reads Hero.MainHero / Hero.OneToOneConversationHero, so the server
    /// cannot rebuild it - but no number crosses the wire: the coefficient and the XP are derived
    /// here from the same vanilla model the host uses.
    ///
    /// The client's own copy of the consequence still runs and is a no-op, because GainRawXpPatch,
    /// SetSkillXpPatch and ChangeSkillLevelPatch all swallow client-side XP writes. Those three
    /// must stay as they are or this double-awards.
    /// </remarks>
    private void ApplyDefectionPersuasionXp(Hero playerHero, DefectionPersuasionOutcome[] outcomes)
    {
        if (playerHero == null || outcomes == null || outcomes.Length == 0) return;

        // TryResolveContext already rejects an over-long list; this is defence in depth.
        var count = Math.Min(outcomes.Length, Patches.LordBarterPatch.MaxDefectionPersuasionOutcomes);

        for (int i = 0; i < count; i++)
        {
            var outcome = outcomes[i];
            if (!Enum.IsDefined(typeof(PersuasionOptionResult), outcome.Result) ||
                !Enum.IsDefined(typeof(PersuasionArgumentStrength), outcome.ArgumentStrength))
            {
                Logger.Warning(
                    "Ignoring defection persuasion outcome with invalid enums (result {Result}, strength {Strength})",
                    outcome.Result,
                    outcome.ArgumentStrength);
                continue;
            }

            var result = (PersuasionOptionResult)outcome.Result;
            if (result != PersuasionOptionResult.Success && result != PersuasionOptionResult.CriticalSuccess)
                continue;

            var strength = (PersuasionArgumentStrength)outcome.ArgumentStrength;

            // Vanilla: coefficient = strength >= 0 ? 50 : |strength| * 50, doubled on a critical.
            int coefficient = (int)strength >= 0 ? 50 : MathF.Abs((int)strength) * 50;
            if (result == PersuasionOptionResult.CriticalSuccess) coefficient *= 2;

            SkillLevelingManager.OnPersuasionSucceeded(
                playerHero,
                DefaultSkills.Charm,
                DefectionPersuasionDifficulty,
                coefficient);
        }
    }
}
