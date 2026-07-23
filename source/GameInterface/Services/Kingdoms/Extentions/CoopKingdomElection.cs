using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Kingdoms.Extentions
{
    public class CoopKingdomElection : KingdomElection
    {
        private float? randomFloat;

        public float RandomFloat
        {
            get
            {
                if (!randomFloat.HasValue)
                {
                    randomFloat = MBRandom.RandomFloat;
                }
                return randomFloat.Value;
            }
        }

        public CoopKingdomElection(KingdomDecision decision, float? randomFloat = null) : base(decision)
        {
            this.randomFloat = randomFloat;
        }

        public void StartElectionCoop()
        {
            this.SetupPlayerVoteElection();
            if (this._decision.ShouldBeCancelled())
            {
                return;
            }
            if (!this.IsPlayerSupporter || this._ignorePlayerSupport)
            {
                this.ReadyToAiChooseCoop();
                return;
            }
            if (this._decision.IsSingleClanDecision())
            {
                this._chosenOutcome = this._possibleOutcomes.FirstOrDefault((DecisionOutcome t) => t.SponsorClan != null && t.SponsorClan == Clan.PlayerClan);
                Supporter supporter = new Supporter(Clan.PlayerClan);
                supporter.SupportWeight = Supporter.SupportWeights.FullyPush;
                this._chosenOutcome.AddSupport(supporter);
            }
        }

        public void SetupPlayerVoteElection()
        {
            this.Setup();
            this.DetermineSupport(this._possibleOutcomes, false);
            this._decision.DetermineSponsors(this._possibleOutcomes);
            this.UpdateSupport(this._possibleOutcomes);
            if (this._decision.ShouldBeCancelled())
            {
                this.IsCancelled = true;
            }
        }

        // TODO : If there are multiple clients in the same clan, only the leader of the clan should vote on issues
        // This logic is intended to support that
        public void ApplyClanVote(Clan clan, int outcomeIndex, Supporter.SupportWeights supportWeight, bool isAbstain)
        {
            Supporter supporter = new Supporter(clan);
            supporter.SupportWeight = supportWeight;

            foreach (DecisionOutcome outcome in this._possibleOutcomes)
            {
                outcome.ResetSupport(supporter);
            }

            if (isAbstain || outcomeIndex < 0 || outcomeIndex >= this._possibleOutcomes.Count)
            {
                if (this._chooser == clan)
                {
                    this._chosenOutcome = null;
                }
                return;
            }

            DecisionOutcome selectedOutcome = this._possibleOutcomes[outcomeIndex];
            if (this._chooser == clan && this._decision.IsKingsVoteAllowed)
            {
                this._chosenOutcome = selectedOutcome;
            }

            selectedOutcome.AddSupport(supporter);
        }

        public DecisionOutcome ResolveWithCurrentVotes()
        {
            DecisionOutcome chosenOutcome = this.ChooseOutcomeWithCurrentVotes();
            this.ApplyChosenOutcomeCoop();
            return chosenOutcome;
        }

        public DecisionOutcome ChooseOutcomeWithCurrentVotes()
        {
            this.DetermineOfficialSupport();
            if (this._chosenOutcome == null)
            {
                this._chosenOutcome = this.GetAiChoiceCoop(this._possibleOutcomes);
            }
            return this._chosenOutcome;
        }

        public void ApplyChosenOutcomeCoop()
        {
            if (this._decision.OnShowDecision())
            {
                this.ApplyChosenOutcome();
            }
        }

        private void ReadyToAiChooseCoop()
        {
            this._chosenOutcome = this.GetAiChoiceCoop(this._possibleOutcomes);
            if (this._decision.OnShowDecision())
            {
                this.ApplyChosenOutcome();
            }
        }

        public DecisionOutcome GetAiChoiceCoop(MBReadOnlyList<DecisionOutcome> possibleOutcomes)
        {
            this.DetermineOfficialSupport();
            DecisionOutcome decisionOutcome = possibleOutcomes.MaxBy((DecisionOutcome t) => t.TotalSupportPoints);
            DecisionOutcome result = decisionOutcome;
            if (this._decision.IsKingsVoteAllowed)
            {
                DecisionOutcome decisionOutcome2 = possibleOutcomes.MaxBy((DecisionOutcome t) => this._decision.DetermineSupport(this._chooser, t));
                float num = this._decision.DetermineSupport(this._chooser, decisionOutcome2);
                float num2 = this._decision.DetermineSupport(this._chooser, decisionOutcome);
                float num3 = num - num2;
                num3 = MathF.Min(num3, this._chooser.Influence);
                if (num3 > 10f)
                {
                    float num4 = 300f + (float)this.GetInfluenceRequiredToOverrideDecision(decisionOutcome, decisionOutcome2);
                    if (num3 > num4)
                    {
                        float num5 = num4 / num3;
                        if (RandomFloat > num5)
                        {
                            result = decisionOutcome2;
                        }
                    }
                }
            }
            return result;
        }
    }
}
