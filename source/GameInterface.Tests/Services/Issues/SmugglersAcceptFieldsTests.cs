using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Migrated.Smugglers;
using GameInterface.Services.Issues.Messages;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

public class SmugglersAcceptFieldsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void AlternativeJournalPreservesProgressAndFailureStatus(int progress)
    {
        var log = new TaleWorlds.CampaignSystem.JournalLog(
            new TaleWorlds.CampaignSystem.CampaignTime(1234500000000L), null, null, progress, 7);
        var fields = new NetworkSmugglersAlternativeJournal("lord-3", "issue_31",
            new[] { new SmugglersJournalEntry(log) }, true,
            TaleWorlds.CampaignSystem.Issues.IssueBase.IssueUpdateDetails.SentTroopsFailedQuest);

        var result = GenericAcceptFieldsSerializer.Deserialize<NetworkSmugglersAlternativeJournal>(
            GenericAcceptFieldsSerializer.Serialize(fields));
        var restored = Assert.Single(result.Entries).ToJournalLog();

        Assert.Equal(fields.IssueId, result.IssueId);
        Assert.Equal(fields.OwnerId, result.OwnerId);
        Assert.Equal(fields.Status, result.Status);
        Assert.True(result.EffectsResolved);
        Assert.Equal(log.LogTime.NumTicks, restored.LogTime.NumTicks);
        Assert.Equal(progress, restored.CurrentProgress);
        Assert.Equal(7, restored.Range);
        Assert.Equal(log.Type, restored.Type);
    }

    [Fact]
    public void QuestAcceptancePreservesAuthoritativePartyPlayerAndDeadline()
    {
        var fields = new SmugglersQuestAcceptFields("issue_31_quest", "party-7", 0.35f, 1800,
            1234567890123L, "hero-2", 1234500000000L);

        var result = GenericAcceptFieldsSerializer.Deserialize<SmugglersQuestAcceptFields>(
            GenericAcceptFieldsSerializer.Serialize(fields));

        Assert.Equal(fields.QuestId, result.QuestId);
        Assert.Equal(fields.PartyId, result.PartyId);
        Assert.Equal(fields.PlayerHeroId, result.PlayerHeroId);
        Assert.Equal(fields.Difficulty, result.Difficulty);
        Assert.Equal(fields.RewardGold, result.RewardGold);
        Assert.Equal(fields.DueTimeTicks, result.DueTimeTicks);
        Assert.Equal(fields.StartLogTimeTicks, result.StartLogTimeTicks);
    }

    [Fact]
    public void IssueCreationPreservesDirectionGenerationAndTiming()
    {
        var fields = new NetworkSmugglersIssueCreated("lord-3", "town_ES1", "town_ES2", 7,
            "issue_31", 1234567890123L, 2345678901234L);

        var result = GenericAcceptFieldsSerializer.Deserialize<NetworkSmugglersIssueCreated>(
            GenericAcceptFieldsSerializer.Serialize(fields));

        Assert.Equal(fields.OwnerId, result.OwnerId);
        Assert.Equal(fields.TargetSettlementId, result.TargetSettlementId);
        Assert.Equal(fields.OriginSettlementId, result.OriginSettlementId);
        Assert.Equal(fields.Generation, result.Generation);
        Assert.Equal(fields.IssueId, result.IssueId);
        Assert.Equal(fields.CreationTimeTicks, result.CreationTimeTicks);
        Assert.Equal(fields.DueTimeTicks, result.DueTimeTicks);
    }
}
