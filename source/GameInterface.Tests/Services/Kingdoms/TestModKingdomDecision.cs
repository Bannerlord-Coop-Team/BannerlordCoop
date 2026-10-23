using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using CampaignKingdomDecision = TaleWorlds.CampaignSystem.Election.KingdomDecision;

namespace GameInterface.Tests.Services.Kingdoms;

/// <summary>
/// A kingdom decision of a type Coop does not know, standing in for one a mod adds.
/// </summary>
public class TestModKingdomDecision : CampaignKingdomDecision
{
    public string Note;
    public int AppliedOutcomes;

    public TestModKingdomDecision(Clan proposerClan) : base(proposerClan)
    {
    }

    public override bool IsAllowed() => true;
    public override int GetProposalInfluenceCost() => 0;
    public override TextObject GetGeneralTitle() => new TextObject("Test mod decision");
    public override TextObject GetSupportTitle() => GetGeneralTitle();
    public override TextObject GetChooseTitle() => GetGeneralTitle();
    public override TextObject GetSupportDescription() => GetGeneralTitle();
    public override TextObject GetChooseDescription() => GetGeneralTitle();
    public override IEnumerable<DecisionOutcome> DetermineInitialCandidates()
    {
        yield return new TestModOutcome();
    }
    public override Clan DetermineChooser() => ProposerClan;
    public override float DetermineSupport(Clan clan, DecisionOutcome possibleOutcome) => 1f;
    public override void DetermineSponsors(MBReadOnlyList<DecisionOutcome> possibleOutcomes) { }
    public override void ApplyChosenOutcome(DecisionOutcome chosenOutcome) => AppliedOutcomes++;
    public override TextObject GetSecondaryEffects() => TextObject.GetEmpty();
    public override void ApplySecondaryEffects(MBReadOnlyList<DecisionOutcome> possibleOutcomes, DecisionOutcome chosenOutcome) { }
    public override TextObject GetChosenOutcomeText(DecisionOutcome chosenOutcome, SupportStatus supportStatus, bool isShortVersion = false) => GetGeneralTitle();
    public override DecisionOutcome GetQueriedDecisionOutcome(MBReadOnlyList<DecisionOutcome> possibleOutcomes) => possibleOutcomes.FirstOrDefault();

    private class TestModOutcome : DecisionOutcome
    {
        public override TextObject GetDecisionTitle() => new TextObject("Test mod outcome");
        public override TextObject GetDecisionDescription() => GetDecisionTitle();
        public override string GetDecisionLink() => null;
        public override ImageIdentifier GetDecisionImageIdentifier() => null;
    }
}
