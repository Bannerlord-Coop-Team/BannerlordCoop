using Common.Util;
using GameInterface.Services.MobilePartyAIs;
using GameInterface.Services.MobilePartyAIs.Patches;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobilePartyAIs;

public sealed class PartyAiBatchRunnerTests
{
    [Fact]
    public void TickParties_ThrowingParty_ContinuesWithRemainingParties()
    {
        MobileParty first = CreateParty();
        MobileParty poisoned = CreateParty();
        MobileParty last = CreateParty();
        var attempts = new List<MobilePartyAi>();
        using var runner = new PartyAiBatchRunner((ai, _) =>
        {
            attempts.Add(ai);
            if (ai == poisoned.Ai)
                throw new InvalidOperationException("poisoned party");
        });

        runner.TickParties(new[] { first, poisoned, last }, 3, 0f);

        Assert.Equal(new[] { first.Ai, poisoned.Ai, last.Ai }, attempts);
    }

    [Fact]
    public void TickParties_MissingAi_ContinuesWithRemainingParties()
    {
        MobileParty missingAi = ObjectHelper.SkipConstructor<MobileParty>();
        MobileParty healthy = CreateParty();
        var attempts = new List<MobilePartyAi>();
        using var runner = new PartyAiBatchRunner((ai, _) => attempts.Add(ai));

        runner.TickParties(new[] { missingAi, healthy }, 2, 0f);

        Assert.Equal(new[] { healthy.Ai }, attempts);
    }

    [Fact]
    public void Dispose_OlderRunner_DoesNotUnbindReplacement()
    {
        var first = new PartyAiBatchRunner();
        var replacement = new PartyAiBatchRunner();

        try
        {
            Assert.Same(replacement, PartiesThinkPatch.BoundRunner);

            first.Dispose();

            Assert.Same(replacement, PartiesThinkPatch.BoundRunner);
        }
        finally
        {
            replacement.Dispose();
            first.Dispose();
        }

        Assert.Null(PartiesThinkPatch.BoundRunner);
    }

    private static MobileParty CreateParty()
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.Ai = new MobilePartyAi(party);
        return party;
    }
}
