using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Configuration;
using GameInterface.Services.MobileParties.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

/// <summary>
/// Covers the record pruning that <see cref="LordDefectionRetryMode.AlwaysRetry"/> depends on.
/// </summary>
/// <remarks>
/// The blocker these guard is NOT <c>CanAttemptToPersuade</c>. It is
/// <c>conversation_lord_from_ruling_clan_on_condition</c>, which refuses on
/// <c>Any(a => a.PersuadedHero == OneToOneConversationHero)</c> - a predicate that checks neither age
/// nor success - and returns before the gate is consulted. So AlwaysRetry only works if this lord's
/// records are gone.
/// </remarks>
public class LordDefectionRetryTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }

    public LordDefectionRetryTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private static PersuasionAttempt Attempt(Hero lord, PersuasionOptionResult result) =>
        new PersuasionAttempt(lord, default, null, result, 0);

    [Fact]
    public void AlwaysRetry_ClearsEveryAttemptAgainstThisLord_IncludingTheSuccessfulOnes()
    {
        var server = TestEnvironment.Server;

        server.Call(() =>
        {
            var lord = GameObjectCreator.CreateInitializedObject<Hero>();
            var otherLord = GameObjectCreator.CreateInitializedObject<Hero>();

            var behavior = new LordDefectionCampaignBehavior();
            behavior._previousDefectionPersuasionAttempts = new List<PersuasionAttempt>
            {
                Attempt(lord, PersuasionOptionResult.Failure),
                // A failed persuasion still leaves its winning options behind. They match the pre-gate's
                // predicate just as a failure does, so they have to go too or the refusal survives.
                Attempt(lord, PersuasionOptionResult.Success),
                Attempt(lord, PersuasionOptionResult.CriticalSuccess),
                Attempt(otherLord, PersuasionOptionResult.Failure),
            };

            LordDefectionRetryPatches.ConversationLordFromRulingClanPatch.ClearAttemptsForRetry(
                behavior, lord, LordDefectionRetryMode.AlwaysRetry);

            var remaining = behavior._previousDefectionPersuasionAttempts;

            Assert.DoesNotContain(remaining, attempt => attempt.PersuadedHero == lord);

            // An unrelated lord's refusal is untouched - this releases one negotiation, not all of them.
            Assert.Single(remaining);
            Assert.Equal(otherLord, remaining[0].PersuadedHero);
        });
    }

    [Theory]
    [InlineData(LordDefectionRetryMode.Vanilla)]
    [InlineData(LordDefectionRetryMode.NeverExpire)]
    public void OtherModes_LeaveTheRecordsAlone(LordDefectionRetryMode mode)
    {
        var server = TestEnvironment.Server;

        server.Call(() =>
        {
            var lord = GameObjectCreator.CreateInitializedObject<Hero>();

            var behavior = new LordDefectionCampaignBehavior();
            behavior._previousDefectionPersuasionAttempts = new List<PersuasionAttempt>
            {
                Attempt(lord, PersuasionOptionResult.Failure),
            };

            LordDefectionRetryPatches.ConversationLordFromRulingClanPatch.ClearAttemptsForRetry(
                behavior, lord, mode);

            // Vanilla needs its own week/year rules to decide; NeverExpire needs the refusal to stand.
            Assert.Single(behavior._previousDefectionPersuasionAttempts);
        });
    }

    [Fact]
    public void AlwaysRetry_WithNothingRecorded_DoesNotThrow()
    {
        var server = TestEnvironment.Server;

        server.Call(() =>
        {
            var lord = GameObjectCreator.CreateInitializedObject<Hero>();
            var behavior = new LordDefectionCampaignBehavior();

            LordDefectionRetryPatches.ConversationLordFromRulingClanPatch.ClearAttemptsForRetry(
                behavior, lord, LordDefectionRetryMode.AlwaysRetry);

            LordDefectionRetryPatches.ConversationLordFromRulingClanPatch.ClearAttemptsForRetry(
                behavior, null, LordDefectionRetryMode.AlwaysRetry);
        });
    }
}
