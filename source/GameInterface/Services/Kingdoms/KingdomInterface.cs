using Common;
using Common.Util;
using GameInterface.Extentions;
using GameInterface.Services.Kingdoms.Extentions;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
namespace GameInterface.Services.Kingdoms;
public interface IKingdomInterface : IGameAbstraction
{
    float AddDecision(Kingdom kingdom, KingdomDecision kingdomDecision, bool ignoreInfluenceCost, float? randomFloat = null, bool applyInfluenceCost = true);
    void RunAddDecision(Kingdom kingdom, KingdomDecision kingdomDecision, bool ignoreInfluenceCost, float randomFloat);
    void RemoveDecision(Kingdom kingdom, KingdomDecision kingdomDecision);
    void ChangeKingdomPolicy(Kingdom kingdom, PolicyObject policy, bool isAdd);
}
internal class KingdomInterface : IKingdomInterface
{
    private readonly IKingdomDecisionVoteManager decisionVoteManager;
    public KingdomInterface(IKingdomDecisionVoteManager decisionVoteManager)
    {
        this.decisionVoteManager = decisionVoteManager;
    }
    public float AddDecision(
        Kingdom kingdom,
        KingdomDecision kingdomDecision,
        bool ignoreInfluenceCost,
        float? randomFloat = null,
        bool applyInfluenceCost = true)
    {
        KingdomRegistry.EnsureRuntimeCollections(kingdom);
        if (applyInfluenceCost && !ignoreInfluenceCost)
        {
            Clan proposerClan = kingdomDecision.ProposerClan;
            int influenceCost = kingdomDecision.GetInfluenceCost(proposerClan);
            ChangeClanInfluenceAction.Apply(proposerClan, (float)(-(float)influenceCost));
        }
        bool isPlayerInvolved = IsCoopPlayerInvolved(kingdomDecision);
        CampaignEventDispatcher.Instance.OnKingdomDecisionAdded(kingdomDecision, isPlayerInvolved);
        var playerParties = Campaign.Current.CampaignObjectManager.GetPlayerMobileParties();
        if (playerParties.All(party => party.ActualClan.Kingdom != kingdomDecision.Kingdom))
        {
            CoopKingdomElection election = new CoopKingdomElection(kingdomDecision, randomFloat);
            election.StartElectionCoop();
            return election.RandomFloat;
        }
        kingdom._unresolvedDecisions.Add(kingdomDecision);
        decisionVoteManager.RegisterDecision(kingdomDecision);
        return default;
    }
    public void RunAddDecision(Kingdom kingdom, KingdomDecision kingdomDecision, bool ignoreInfluenceCost, float randomFloat)
    {
        RunKingdomMutation(() =>
        {
            AddDecision(kingdom, kingdomDecision, ignoreInfluenceCost, randomFloat, ModInformation.IsServer);
        });
    }
    public void RemoveDecision(Kingdom kingdom, KingdomDecision kingdomDecision)
    {
        RunKingdomMutation(() =>
        {
            using (new AllowedThread())
            {
                kingdom.RemoveDecision(kingdomDecision);
            }
        });
    }
    public void ChangeKingdomPolicy(Kingdom kingdom, PolicyObject policy, bool isAdd)
    {
        RunKingdomMutation(() =>
        {
            using (new AllowedThread())
            {
                KingdomRegistry.EnsureRuntimeCollections(kingdom);
                if (isAdd)
                {
                    kingdom.AddPolicy(policy);
                }
                else
                {
                    kingdom.RemovePolicy(policy);
                }
            }
        });
    }
    private bool IsCoopPlayerInvolved(KingdomDecision kingdomDecision)
    {
        if (decisionVoteManager.HasEligiblePlayerClan(kingdomDecision))
        {
            return true;
        }
        var playerParties = Campaign.Current?.CampaignObjectManager?.GetPlayerMobileParties();
        if (playerParties == null) return false;
        return playerParties.Any(party => party?.ActualClan?.Kingdom == kingdomDecision.Kingdom);
    }
    private static void RunKingdomMutation(Action action)
    {
        if (!GameThread.Instance.IsInitialized)
        {
            action();
            return;
        }
        GameThread.RunSafe(action, blocking: true, context: nameof(KingdomInterface));
    }
}
