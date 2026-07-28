using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Kingdoms.Extentions;
using GameInterface.Services.Kingdoms.Messages;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
namespace GameInterface.Services.Kingdoms;
public interface IKingdomInterface : IGameAbstraction
{
    bool AddDecisionPrefix(Kingdom kingdom, KingdomDecision kingdomDecision, bool ignoreInfluenceCost);
    bool RemoveDecisionPrefix(Kingdom kingdom, KingdomDecision kingdomDecision);
    bool AddPolicyPrefix(Kingdom kingdom, PolicyObject policy);
    bool RemovePolicyPrefix(Kingdom kingdom, PolicyObject policy);
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
    public bool AddDecisionPrefix(Kingdom kingdom, KingdomDecision kingdomDecision, bool ignoreInfluenceCost)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient)
        {
            float clientRandomNumber = AddDecision(kingdom, kingdomDecision, ignoreInfluenceCost, applyInfluenceCost: false);
            MessageBroker.Instance.Publish(kingdom,
                new DecisionAdded(kingdom, kingdomDecision, ignoreInfluenceCost, clientRandomNumber));
            return false;
        }
        float randomNumber = AddDecision(kingdom, kingdomDecision, ignoreInfluenceCost, applyInfluenceCost: true);
        MessageBroker.Instance.Publish(kingdom,
            new DecisionAdded(kingdom, kingdomDecision, ignoreInfluenceCost, randomNumber));
        return false;
    }
    public bool RemoveDecisionPrefix(Kingdom kingdom, KingdomDecision kingdomDecision)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return false;
        KingdomRegistry.EnsureRuntimeCollections(kingdom);
        var index = kingdom._unresolvedDecisions?.FindIndex(decision => decision == kingdomDecision) ?? -1;
        if (index >= 0)
        {
            MessageBroker.Instance.Publish(kingdom,
                new DecisionRemoved(kingdom, index));
        }
        return true;
    }
    public bool AddPolicyPrefix(Kingdom kingdom, PolicyObject policy)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return false;
        KingdomRegistry.EnsureRuntimeCollections(kingdom);
        if (!kingdom._activePolicies.Contains(policy))
        {
            MessageBroker.Instance.Publish(kingdom, new KingdomPolicyChanged(kingdom, policy, isAdd: true));
        }
        return true;
    }
    public bool RemovePolicyPrefix(Kingdom kingdom, PolicyObject policy)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return false;
        KingdomRegistry.EnsureRuntimeCollections(kingdom);
        if (kingdom._activePolicies.Contains(policy))
        {
            MessageBroker.Instance.Publish(kingdom, new KingdomPolicyChanged(kingdom, policy, isAdd: false));
        }
        return true;
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
        bool hasEligiblePlayerClan = decisionVoteManager.HasEligiblePlayerClan(kingdomDecision);
        CampaignEventDispatcher.Instance.OnKingdomDecisionAdded(kingdomDecision, hasEligiblePlayerClan);
        if (!hasEligiblePlayerClan)
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
