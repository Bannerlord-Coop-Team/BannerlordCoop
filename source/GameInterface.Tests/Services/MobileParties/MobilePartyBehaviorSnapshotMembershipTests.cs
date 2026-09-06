using Common.Util;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Services.SiegeEvents;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.MobileParties;

[Collection(nameof(CampaignCurrentCollection))]
public class MobilePartyBehaviorSnapshotMembershipTests : IDisposable
{
    private readonly Campaign previousCampaign = Campaign.Current;
    private readonly Campaign campaign = ObjectHelper.SkipConstructor<Campaign>();
    private readonly Mock<IObjectManager> registry = new();
    private readonly MobilePartyBehaviorSnapshot snapshot;

    public MobilePartyBehaviorSnapshotMembershipTests()
    {
        campaign.CampaignObjectManager = new CampaignObjectManager
        {
            Settlements = new MBReadOnlyList<Settlement>(new List<Settlement>()),
        };
        Campaign.Current = campaign;
        snapshot = new MobilePartyBehaviorSnapshot(registry.Object);
    }

    public void Dispose() => Campaign.Current = previousCampaign;

    [Fact]
    public void TryApplyJoinBaseline_MatchingEmptyMembership_DoesNotLogDiagnostics()
    {
        Assert.True(snapshot.TryApplyJoinBaseline(Array.Empty<MobilePartyJoinState>(), () => { }));
        Assert.Null(snapshot.LastJoinBaselineMembership);
        Assert.Equal(0, snapshot.LoggedJoinBaselineMembershipCount);
    }

    [Fact]
    public void TryApplyJoinBaseline_MatchingMembershipWithInvalidBehavior_DoesNotReportFalseDifferences()
    {
        Party("Created_42");
        Reject(State("Created_42"));
        Assert.Equal("state 0 party 'Created_42' failed validation: party AI is unavailable", snapshot.LastJoinBaselineFailure);
        Assert.Contains("missing=0, inactive=0, outsideCollection=0, extraActive=0", snapshot.LastJoinBaselineMembership);
        Assert.Contains("detailsShown=0/0", snapshot.LastJoinBaselineMembership);
    }

    [Fact]
    public void TryApplyJoinBaseline_MissingObject_IdentifiesBaselineIdWithoutApplying()
    {
        bool applied = false;
        Assert.False(snapshot.TryApplyJoinBaseline(new[] { State("Created_42") }, () => applied = true));
        Assert.False(applied);
        Assert.Equal("party count mismatch (baseline=1, client=0)", snapshot.LastJoinBaselineFailure);
        Assert.Contains("missing=1, inactive=0, outsideCollection=0, extraActive=0", snapshot.LastJoinBaselineMembership);
        Assert.Contains("missing baseline id: Created_42", snapshot.LastJoinBaselineMembership);
    }

    [Fact]
    public void TryApplyJoinBaseline_InactiveObject_ReportsPlayerAndSettlementInsteadOfMissing()
    {
        MobileParty party = Party("Player", active: false);
        campaign.MainParty = party;
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        settlement.StringId = "town_ES1";
        party._currentSettlement = settlement;

        Reject(State("Player"));

        Assert.Contains("missing=0, inactive=1, outsideCollection=0", snapshot.LastJoinBaselineMembership);
        Assert.Contains("inactive baseline id: Player", snapshot.LastJoinBaselineMembership);
        Assert.Contains("main=True", snapshot.LastJoinBaselineMembership);
        Assert.Contains("settlement=town_ES1", snapshot.LastJoinBaselineMembership);
        Assert.False(party.IsActive);
        Assert.Same(settlement, party.CurrentSettlement);
    }

    [Fact]
    public void TryApplyJoinBaseline_RegisteredObjectOutsideCollection_DistinguishesItFromMissing()
    {
        MobileParty party = Party("Created_42");
        campaign.CampaignObjectManager._mobileParties.Remove(party);
        Reject(State("Created_42"));
        Assert.Contains("missing=0, inactive=0, outsideCollection=1", snapshot.LastJoinBaselineMembership);
        Assert.Contains("baseline object outside local collection: Created_42", snapshot.LastJoinBaselineMembership);
    }

    [Fact]
    public void TryApplyJoinBaseline_EqualCountDifferentMembership_ReportsMissingAndExtra()
    {
        Party("Created_43");
        Reject(State("Created_42"));
        Assert.Equal("state 0 references missing mobile party 'Created_42'", snapshot.LastJoinBaselineFailure);
        Assert.Contains("missing=1, inactive=0, outsideCollection=0, extraActive=1", snapshot.LastJoinBaselineMembership);
        Assert.Contains("active local absent from baseline: id=MobileParty_Created_43", snapshot.LastJoinBaselineMembership);
    }

    [Fact]
    public void TryApplyJoinBaseline_Duplicates_ReportsBaselineIdsAndLocalMembership()
    {
        MobileParty party = Party("Created_42");
        campaign.CampaignObjectManager._mobileParties.Add(party);
        Reject(State("Created_42"), State("Created_42"));
        Assert.Contains("duplicateBaselineIds=1", snapshot.LastJoinBaselineMembership);
        Assert.Contains("duplicateLocalMembership=1", snapshot.LastJoinBaselineMembership);
        Assert.Contains("duplicate baseline id: Created_42", snapshot.LastJoinBaselineMembership);
    }

    [Fact]
    public void TryApplyJoinBaseline_RegistryAliasesAndUnregisteredActive_AreReported()
    {
        MobileParty party = Party("Created_42");
        registry.Setup(m => m.TryGetObject("Alias", out party)).Returns(true);
        object alias = party;
        registry.Setup(m => m.TryGetObject<object>("MobileParty_Alias", out alias)).Returns(true);
        MobileParty other = Party("Created_43");
        string duplicateId = "MobileParty_Created_42";
        registry.Setup(m => m.TryGetId(other, out duplicateId)).Returns(true);
        MobileParty unregistered = Party("Created_44");
        string missingId = null!;
        registry.Setup(m => m.TryGetId(unregistered, out missingId)).Returns(false);
        Reject(State("Created_42"), State("Alias"));
        Assert.Contains("aliasedBaselineIds=1", snapshot.LastJoinBaselineMembership);
        Assert.Contains("duplicateLocalIds=1, unregisteredActive=1", snapshot.LastJoinBaselineMembership);
    }

    [Fact]
    public void TryApplyJoinBaseline_MovementAndOrderChanges_SuppressIdenticalMembership()
    {
        Reject(State("Created_42"), State("Created_43"));
        string first = snapshot.LastJoinBaselineMembership;
        MobilePartyJoinState moved = State("Created_42");
        moved.Bearing = new Vec2(30f, 40f);
        PartyBehaviorUpdateData behavior = moved.Behavior;
        behavior.PartyPosition = new CampaignVec2(new Vec2(70f, 80f), true);
        moved.Behavior = behavior;
        Reject(State("Created_43"), moved);
        Assert.Equal(first, snapshot.LastJoinBaselineMembership);
        Assert.Equal(1, snapshot.LoggedJoinBaselineMembershipCount);
    }

    [Fact]
    public void TryApplyJoinBaseline_LargeMismatch_BoundsDetailsAndIdentifierLength()
    {
        var states = Enumerable.Range(0, 100)
            .Select(i => State($"{i:D3}_" + new string('x', 1000))).ToArray();
        Reject(states);
        Assert.Contains("missing=100", snapshot.LastJoinBaselineMembership);
        Assert.Contains("detailsShown=16/100 (limit=16)", snapshot.LastJoinBaselineMembership);
        Assert.True(snapshot.LastJoinBaselineMembership.Length < 3000);
    }

    [Fact]
    public void TryApplyJoinBaseline_ChangingFailures_BoundsReportsAndSuccessResetsBudget()
    {
        for (int i = 0; i < 20; i++) Reject(State($"Created_{i}"));
        Assert.Equal(8, snapshot.LoggedJoinBaselineMembershipCount);
        Assert.True(snapshot.TryApplyJoinBaseline(Array.Empty<MobilePartyJoinState>(), () => { }));
        Assert.Equal(0, snapshot.LoggedJoinBaselineMembershipCount);
        Assert.Null(snapshot.LastJoinBaselineMembership);
        Reject(State("Created_0"));
        Assert.Equal(1, snapshot.LoggedJoinBaselineMembershipCount);
    }

    [Fact]
    public void TryApplyJoinBaseline_DiagnosticLookupThrows_PreservesOriginalRejectionAndSuppressesFailure()
    {
        object party = null!;
        registry.Setup(m => m.TryGetObject<object>("MobileParty_Created_42", out party)).Throws<InvalidOperationException>();
        Reject(State("Created_42"));
        Reject(State("Created_42"));
        Assert.Equal("party count mismatch (baseline=1, client=0)", snapshot.LastJoinBaselineFailure);
        Assert.Equal(1, snapshot.LoggedJoinBaselineMembershipCount);
    }

    [Fact]
    public void TryApplyJoinBaseline_WrongTypeRegistration_DoesNotUseErrorLoggingTypedLookup()
    {
        object registered = new object();
        registry.Setup(m => m.TryGetObject<object>("MobileParty_Created_42", out registered)).Returns(true);
        Reject(State("Created_42"));
        Reject(State("Created_42"));
        Assert.Contains("wrongType=1", snapshot.LastJoinBaselineMembership);
        Assert.Contains("wrong-type baseline id: Created_42 type=Object", snapshot.LastJoinBaselineMembership);
        Assert.Equal(1, snapshot.LoggedJoinBaselineMembershipCount);
        MobileParty party = null!;
        registry.Verify(m => m.TryGetObject("Created_42", out party), Times.Never);
    }

    private void Reject(params MobilePartyJoinState[] states) =>
        Assert.False(snapshot.TryApplyJoinBaseline(states, () => throw new InvalidOperationException("must not apply")));

    private MobileParty Party(string id, bool active = true)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        party.StringId = "MobileParty_" + id;
        party.IsActive = active;
        campaign.CampaignObjectManager._mobileParties.Add(party);
        string registryId = "MobileParty_" + id;
        registry.Setup(m => m.TryGetId(party, out registryId)).Returns(true);
        registry.Setup(m => m.TryGetObject(id, out party)).Returns(true);
        object registered = party;
        registry.Setup(m => m.TryGetObject<object>(registryId, out registered)).Returns(true);
        return party;
    }

    private static MobilePartyJoinState State(string id) => new()
    {
        Behavior = new PartyBehaviorUpdateData(id, AiBehavior.Hold, null, default, default, AiBehavior.Hold, default, default),
    };
}
