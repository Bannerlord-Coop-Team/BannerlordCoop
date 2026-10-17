using Common.Util;
using GameInterface.Services.MapEvents;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class ConversationRestartContextTrackerTests
{
    [Fact]
    public void Consume_SameExistingEncounter_AllowsVanillaReplacement()
    {
        var tracker = new ConversationRestartContextTracker();
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        var requestId = tracker.Capture(encounter);

        var decision = tracker.Consume(requestId, encounter, defender: null, attacker: null);

        Assert.Equal(ConversationRestartDecision.Apply, decision);
    }

    [Fact]
    public void Consume_EncounterChangedAfterRequest_RejectsStaleApproval()
    {
        var tracker = new ConversationRestartContextTracker();
        var capturedEncounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        var currentEncounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        var requestId = tracker.Capture(capturedEncounter);

        var decision = tracker.Consume(requestId, currentEncounter, defender: null, attacker: null);

        Assert.Equal(ConversationRestartDecision.Stale, decision);
    }

    [Fact]
    public void Consume_SameEncounterReinitializedAfterRequest_RejectsStaleApproval()
    {
        var tracker = new ConversationRestartContextTracker();
        var originalTarget = ObjectHelper.SkipConstructor<PartyBase>();
        var replacementTarget = ObjectHelper.SkipConstructor<PartyBase>();
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        encounter._encounteredParty = originalTarget;
        var requestId = tracker.Capture(encounter);

        encounter._encounteredParty = replacementTarget;
        var decision = tracker.Consume(requestId, encounter, defender: originalTarget, attacker: null);

        Assert.Equal(ConversationRestartDecision.Stale, decision);
    }

    [Fact]
    public void Consume_OlderRetryForUnchangedEncounter_AllowsApproval()
    {
        var tracker = new ConversationRestartContextTracker();
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        var olderRequestId = tracker.Capture(encounter);
        var latestRequestId = tracker.Capture(encounter);

        var olderDecision = tracker.Consume(olderRequestId, encounter, defender: null, attacker: null);
        var latestDecision = tracker.Consume(latestRequestId, encounter, defender: null, attacker: null);

        Assert.Equal(ConversationRestartDecision.Apply, olderDecision);
        Assert.Equal(ConversationRestartDecision.Apply, latestDecision);
    }

    [Fact]
    public void Consume_RepeatedApprovalForOpenTarget_IsDuplicate()
    {
        var tracker = new ConversationRestartContextTracker();
        var target = ObjectHelper.SkipConstructor<PartyBase>();
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        encounter._encounteredParty = target;

        var decision = tracker.Consume("already-consumed", encounter, target, attacker: null);

        Assert.Equal(ConversationRestartDecision.Duplicate, decision);
    }

    [Fact]
    public void Consume_ServerDetectedApprovalWithNoEncounter_Applies()
    {
        var tracker = new ConversationRestartContextTracker();

        var decision = tracker.Consume(requestId: null, currentEncounter: null, defender: null, attacker: null);

        Assert.Equal(ConversationRestartDecision.Apply, decision);
    }
}
