using Common.Util;
using GameInterface.Services.MobileParties.Patches;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

public class OnPartyInteractionPatchTests
{
    [Fact]
    public void GetEffectiveInteractionTargetParty_AttachedPartyEngagedByOtherParty_UsesAttachedLeader()
    {
        var attachedParty = CreateParty();
        var leaderParty = CreateParty();
        var engagingParty = CreateParty();

        attachedParty._attachedTo = leaderParty;

        var targetParty = OnPartyInteractionPatch.GetEffectiveInteractionTargetParty(attachedParty, engagingParty);

        Assert.Same(leaderParty, targetParty);
    }

    [Fact]
    public void GetEffectiveInteractionTargetParty_AttachedPartyEngagedByLeader_UsesOriginalParty()
    {
        var attachedParty = CreateParty();
        var leaderParty = CreateParty();

        attachedParty._attachedTo = leaderParty;

        var targetParty = OnPartyInteractionPatch.GetEffectiveInteractionTargetParty(attachedParty, leaderParty);

        Assert.Same(attachedParty, targetParty);
    }

    private static MobileParty CreateParty()
        => ObjectHelper.SkipConstructor<MobileParty>();
}
