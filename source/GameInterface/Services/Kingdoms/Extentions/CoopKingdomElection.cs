using System.Linq;
using Common;
using GameInterface.Services.Clans.Extensions;
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
            // The synchronized kingdom decision is the player-facing prompt. Native's peace
            // notification checks process-local player singletons and must not run again when
            // the explicit co-op vote is resolved on the authoritative server.
            if (IsPendingPlayerPeaceOffer(this._decision) || this._decision.OnShowDecision())
            {
                this.ApplyChosenOutcome();
            }
        }

        private void ReadyToAiChooseCoop()
        {
            this._chosenOutcome = this.GetAiChoiceCoop(this._possibleOutcomes);
            if (TryRedirectPlayerPeaceOffer(this._decision, this._chosenOutcome))
            {
                return;
            }
            if (this._decision.OnShowDecision())
            {
                this.ApplyChosenOutcome();
            }
        }

        /// <summary>
        /// An NPC kingdom deciding that it wants peace is only an offer when the opposing
        /// kingdom contains a player clan. Mirror that offer into the player's kingdom so
        /// the existing synchronized kingdom vote decides whether peace is accepted.
        /// </summary>
        internal static bool TryRedirectPlayerPeaceOffer(KingdomDecision decision, DecisionOutcome chosenOutcome)
        {
            if (decision is not MakePeaceKingdomDecision peaceDecision
                || peaceDecision._isProposedByOpponent
                || peaceDecision.FactionToMakePeaceWith is not Kingdom playerKingdom
                || !playerKingdom.Clans.Any(clan => clan.IsPlayerClan()))
            {
                return false;
            }

            // Consume the original NPC decision on every instance. Only the authoritative
            // server authors the target-side offer that is then replicated to clients.
            if (chosenOutcome is not MakePeaceKingdomDecision.MakePeaceDecisionOutcome { ShouldPeaceBeDeclared: true }
                || !ModInformation.IsServer)
            {
                return true;
            }

            Kingdom proposingKingdom = peaceDecision.Kingdom;
            bool offerAlreadyPending = playerKingdom.UnresolvedDecisions
                .OfType<MakePeaceKingdomDecision>()
                .Any(existing => existing._isProposedByOpponent
                                 && existing.FactionToMakePeaceWith == proposingKingdom);
            if (offerAlreadyPending || playerKingdom.RulingClan == null)
            {
                return true;
            }

            var playerDecision = new MakePeaceKingdomDecision(
                playerKingdom.RulingClan,
                proposingKingdom,
                -peaceDecision.DailyTributeToBePaid,
                peaceDecision.DailyTributeDurationInDays,
                applyResults: true,
                isProposedByOpponent: true);

            playerKingdom.AddDecision(playerDecision, ignoreInfluenceCost: true);
            return true;
        }

        internal static bool IsPendingPlayerPeaceOffer(KingdomDecision decision)
        {
            return decision is MakePeaceKingdomDecision { _isProposedByOpponent: true }
                   && decision.Kingdom?.Clans.Any(clan => clan.IsPlayerClan()) == true;
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
