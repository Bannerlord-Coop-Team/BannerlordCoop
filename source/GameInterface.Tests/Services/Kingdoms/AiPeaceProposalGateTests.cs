using GameInterface.Services.Kingdoms;
using GameInterface.Services.StanceLinks;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.Kingdoms;

/// <summary>
/// The rules that decide whether an AI clan may propose peace, exercised without the campaign
/// statics the live gate reads: the minimum war duration, the post-decline cooldown, and the
/// mirrored-offer duplicate guard. The cooldown state is per instance, so each test gets its own
/// gate and nothing here depends on test ordering or on tests running one at a time.
/// </summary>
public sealed class AiPeaceProposalGateTests
{
    private const string FactionPairKey = "kingdom_a_kingdom_b";
    private const int CooldownDays = 3;

    private readonly AiPeaceProposalGate gate = new AiPeaceProposalGate();

    /// <summary>Zero is the vanilla-faithful default and must never hold a proposal back, not even
    /// on the day the war was declared.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    public void MinimumOfZero_NeverBlocks(double warAgeInDays)
    {
        Assert.False(AiPeaceProposalGate.IsWarTooYoung(warAgeInDays, minimumWarDurationDays: 0, involvesPlayerFaction: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    [InlineData(29.9)]
    public void WarYoungerThanTheMinimum_IsBlocked(double warAgeInDays)
    {
        Assert.True(AiPeaceProposalGate.IsWarTooYoung(warAgeInDays, minimumWarDurationDays: 30, involvesPlayerFaction: true));
    }

    /// <summary>The boundary is inclusive: a war that has run exactly the minimum is old enough.</summary>
    [Theory]
    [InlineData(30)]
    [InlineData(31)]
    public void WarAtOrPastTheMinimum_IsNotBlocked(double warAgeInDays)
    {
        Assert.False(AiPeaceProposalGate.IsWarTooYoung(warAgeInDays, minimumWarDurationDays: 30, involvesPlayerFaction: true));
    }

    /// <summary>AI-vs-AI wars stay vanilla however high the minimum is set.</summary>
    [Fact]
    public void WarWithoutAPlayerFaction_IsNeverBlocked()
    {
        Assert.False(AiPeaceProposalGate.IsWarTooYoung(0, minimumWarDurationDays: 30, involvesPlayerFaction: false));
    }

    /// <summary>A war that is not running reads as unbounded age, so it cannot be blocked.</summary>
    [Fact]
    public void UnboundedWarAge_IsNotBlocked()
    {
        Assert.False(AiPeaceProposalGate.IsWarTooYoung(double.MaxValue, minimumWarDurationDays: 30, involvesPlayerFaction: true));
    }

    [Fact]
    public void PairWithNoRecordedDecline_IsNotOnCooldown()
    {
        Assert.False(gate.IsWithinDeclineCooldown(FactionPairKey, nowInDays: 100, CooldownDays));
    }

    /// <summary>Zero is the vanilla-faithful default: a declined offer may be re-proposed at once.</summary>
    [Fact]
    public void CooldownOfZero_NeverBlocks()
    {
        gate.RecordDecline(FactionPairKey, nowInDays: 100, CooldownDays);

        Assert.False(gate.IsWithinDeclineCooldown(FactionPairKey, nowInDays: 100, cooldownDays: 0));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(102.9)]
    public void DeclinedOffer_BlocksUntilTheCooldownElapses(double nowInDays)
    {
        gate.RecordDecline(FactionPairKey, nowInDays: 100, CooldownDays);

        Assert.True(gate.IsWithinDeclineCooldown(FactionPairKey, nowInDays, CooldownDays));
    }

    [Theory]
    [InlineData(103)]
    [InlineData(150)]
    public void DeclinedOffer_StopsBlockingOnceTheCooldownElapses(double nowInDays)
    {
        gate.RecordDecline(FactionPairKey, nowInDays: 100, CooldownDays);

        Assert.False(gate.IsWithinDeclineCooldown(FactionPairKey, nowInDays, CooldownDays));
    }

    [Fact]
    public void DeclineCooldown_IsPerFactionPair()
    {
        gate.RecordDecline(FactionPairKey, nowInDays: 100, CooldownDays);

        Assert.False(gate.IsWithinDeclineCooldown("kingdom_c_kingdom_d", nowInDays: 100, CooldownDays));
    }

    /// <summary>A later decline restarts the cooldown rather than keeping the first one's expiry.</summary>
    [Fact]
    public void SecondDecline_RestartsTheCooldown()
    {
        gate.RecordDecline(FactionPairKey, nowInDays: 100, CooldownDays);
        gate.RecordDecline(FactionPairKey, nowInDays: 104, CooldownDays);

        Assert.True(gate.IsWithinDeclineCooldown(FactionPairKey, nowInDays: 106, CooldownDays));
        Assert.False(gate.IsWithinDeclineCooldown(FactionPairKey, nowInDays: 107, CooldownDays));
    }

    /// <summary>Recording a decline drops the pairs whose cooldown has run out, so the query stays a
    /// pure read and the dictionary cannot grow for the whole session.</summary>
    [Fact]
    public void RecordingADecline_DropsThePairsWhoseCooldownHasElapsed()
    {
        gate.RecordDecline(FactionPairKey, nowInDays: 100, CooldownDays);
        gate.RecordDecline("kingdom_c_kingdom_d", nowInDays: 200, CooldownDays);

        Assert.False(gate.IsWithinDeclineCooldown(FactionPairKey, nowInDays: 100, CooldownDays));
    }

    [Fact]
    public void ClearDeclineCooldowns_DropsEveryRecordedDecline()
    {
        gate.RecordDecline(FactionPairKey, nowInDays: 100, CooldownDays);

        gate.ClearDeclineCooldowns();

        Assert.False(gate.IsWithinDeclineCooldown(FactionPairKey, nowInDays: 100, CooldownDays));
    }

    /// <summary>The cooldown is keyed by the shared faction-pair key, so which side proposed does
    /// not matter: a decline recorded for A/B has to block B/A too.</summary>
    [Fact]
    public void FactionPairKey_IsTheSameInBothDirections()
    {
        var factionA = CreateFaction("kingdom_a", 1u);
        var factionB = CreateFaction("kingdom_b", 2u);

        Assert.Equal(
            StanceLinkHandler.GetStanceLinkKey(factionA, factionB),
            StanceLinkHandler.GetStanceLinkKey(factionB, factionA));
    }

    [Fact]
    public void DeclineRecordedInOneDirection_BlocksTheOther()
    {
        var factionA = CreateFaction("kingdom_a", 1u);
        var factionB = CreateFaction("kingdom_b", 2u);

        gate.RecordDecline(StanceLinkHandler.GetStanceLinkKey(factionA, factionB), nowInDays: 100, CooldownDays);

        Assert.True(gate.IsWithinDeclineCooldown(
            StanceLinkHandler.GetStanceLinkKey(factionB, factionA),
            nowInDays: 101,
            CooldownDays));
    }

    [Fact]
    public void KingdomWithNoDecisionList_HasNoPendingMirroredOffer()
    {
        var proposingKingdom = CreateKingdom("kingdom_a", 1u);
        var targetKingdom = CreateKingdom("kingdom_b", 2u, withDecisionList: false);

        Assert.False(AiPeaceProposalGate.HasPendingMirroredOffer(targetKingdom, proposingKingdom));
    }

    [Fact]
    public void ClanTargetFaction_HasNoPendingMirroredOffer()
    {
        var proposingKingdom = CreateKingdom("kingdom_a", 1u);

        Assert.False(AiPeaceProposalGate.HasPendingMirroredOffer(CreateFaction("clan_a", 3u), proposingKingdom));
    }

    /// <summary>Only the mirrored offers count. An offer the target itself authored is a normal
    /// proposal and must not suppress the incoming one.</summary>
    [Fact]
    public void OfferTheTargetAuthoredItself_IsNotAPendingMirroredOffer()
    {
        var proposingKingdom = CreateKingdom("kingdom_a", 1u);
        var targetKingdom = CreateKingdom("kingdom_b", 2u);
        AddPeaceDecision(targetKingdom, proposingKingdom, isProposedByOpponent: false);

        Assert.False(AiPeaceProposalGate.HasPendingMirroredOffer(targetKingdom, proposingKingdom));
    }

    /// <summary>A mirrored offer aimed at some third kingdom says nothing about this pair.</summary>
    [Fact]
    public void MirroredOfferForAnotherFaction_IsNotAPendingMirroredOffer()
    {
        var proposingKingdom = CreateKingdom("kingdom_a", 1u);
        var targetKingdom = CreateKingdom("kingdom_b", 2u);
        var otherKingdom = CreateKingdom("kingdom_c", 3u);
        AddPeaceDecision(targetKingdom, otherKingdom, isProposedByOpponent: true);

        Assert.False(AiPeaceProposalGate.HasPendingMirroredOffer(targetKingdom, proposingKingdom));
    }

    [Fact]
    public void MirroredOfferFromTheProposer_IsAPendingMirroredOffer()
    {
        var proposingKingdom = CreateKingdom("kingdom_a", 1u);
        var targetKingdom = CreateKingdom("kingdom_b", 2u);
        AddPeaceDecision(targetKingdom, proposingKingdom, isProposedByOpponent: true);

        Assert.True(AiPeaceProposalGate.HasPendingMirroredOffer(targetKingdom, proposingKingdom));
    }

    /// <summary>The arguments are the target first, then the proposer. Swapping them looks in the
    /// proposer's own queue, which is not where a mirrored offer is ever held.</summary>
    [Fact]
    public void PendingMirroredOffer_IsNotFoundWithTheArgumentsSwapped()
    {
        var proposingKingdom = CreateKingdom("kingdom_a", 1u);
        var targetKingdom = CreateKingdom("kingdom_b", 2u);
        AddPeaceDecision(targetKingdom, proposingKingdom, isProposedByOpponent: true);

        Assert.False(AiPeaceProposalGate.HasPendingMirroredOffer(proposingKingdom, targetKingdom));
    }

    [Fact]
    public void NullFaction_ShortCircuitsTheComposedGate()
    {
        var proposingKingdom = CreateKingdom("kingdom_a", 1u);

        Assert.False(gate.IsPeaceProposalBlocked(null, proposingKingdom, out var blockingRuleWithoutProposer));
        Assert.Equal(PeaceProposalBlock.None, blockingRuleWithoutProposer);

        Assert.False(gate.IsPeaceProposalBlocked(proposingKingdom, null, out var blockingRuleWithoutTarget));
        Assert.Equal(PeaceProposalBlock.None, blockingRuleWithoutTarget);
    }

    /// <summary>Both configurable rules are off by default, so the composed gate only reports the
    /// mirrored-offer guard.</summary>
    [Fact]
    public void ComposedGate_ReportsThePendingMirroredOfferRule()
    {
        var proposingKingdom = CreateKingdom("kingdom_a", 1u);
        var targetKingdom = CreateKingdom("kingdom_b", 2u);
        AddPeaceDecision(targetKingdom, proposingKingdom, isProposedByOpponent: true);

        Assert.True(gate.IsPeaceProposalBlocked(proposingKingdom, targetKingdom, out var blockingRule));
        Assert.Equal(PeaceProposalBlock.PendingMirroredOffer, blockingRule);
    }

    [Fact]
    public void ComposedGate_DoesNotBlockAPairWithNothingPending()
    {
        var proposingKingdom = CreateKingdom("kingdom_a", 1u);
        var targetKingdom = CreateKingdom("kingdom_b", 2u);

        Assert.False(gate.IsPeaceProposalBlocked(proposingKingdom, targetKingdom, out var blockingRule));
        Assert.Equal(PeaceProposalBlock.None, blockingRule);
    }

    /// <summary>Only a mirrored MakePeace decision starts a cooldown. Anything else the sweep hands
    /// over has to leave the recorded declines untouched.</summary>
    [Fact]
    public void RecordDeclinedOffer_IgnoresDecisionsThatAreNotMirroredPeaceOffers()
    {
        var proposingKingdom = CreateKingdom("kingdom_a", 1u);
        var targetKingdom = CreateKingdom("kingdom_b", 2u);
        string factionPairKey = StanceLinkHandler.GetStanceLinkKey(targetKingdom, proposingKingdom);

        gate.RecordDeclinedOffer(null);
        gate.RecordDeclinedOffer(CreatePeaceDecision(targetKingdom, proposingKingdom, isProposedByOpponent: false));

        Assert.False(gate.IsWithinDeclineCooldown(factionPairKey, nowInDays: 0, CooldownDays));
    }

    private static void AddPeaceDecision(Kingdom kingdom, IFaction factionToMakePeaceWith, bool isProposedByOpponent)
    {
        kingdom._unresolvedDecisions.Add(CreatePeaceDecision(kingdom, factionToMakePeaceWith, isProposedByOpponent));
    }

    /// <summary>Both offer fields are readonly on the game type, so they are set the same way
    /// <c>MakePeaceKingdomDecisionData</c> sets them when it rebuilds a received decision.</summary>
    private static MakePeaceKingdomDecision CreatePeaceDecision(Kingdom kingdom, IFaction factionToMakePeaceWith, bool isProposedByOpponent)
    {
        var decision = (MakePeaceKingdomDecision)FormatterServices.GetUninitializedObject(typeof(MakePeaceKingdomDecision));
        decision._kingdom = kingdom;
        GetDecisionField(nameof(MakePeaceKingdomDecision.FactionToMakePeaceWith)).SetValue(decision, factionToMakePeaceWith);
        GetDecisionField("_isProposedByOpponent").SetValue(decision, isProposedByOpponent);
        return decision;
    }

    private static FieldInfo GetDecisionField(string name) => typeof(MakePeaceKingdomDecision).GetField(
        name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static Kingdom CreateKingdom(string stringId, uint id, bool withDecisionList = true)
    {
        var kingdom = (Kingdom)FormatterServices.GetUninitializedObject(typeof(Kingdom));
        kingdom.StringId = stringId;
        kingdom.Id = new MBGUID(id);
        if (withDecisionList)
        {
            kingdom._unresolvedDecisions = new MBList<TaleWorlds.CampaignSystem.Election.KingdomDecision>();
        }

        return kingdom;
    }

    private static IFaction CreateFaction(string stringId, uint id)
    {
        var clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
        clan.StringId = stringId;
        clan.Id = new MBGUID(id);
        return clan;
    }
}
