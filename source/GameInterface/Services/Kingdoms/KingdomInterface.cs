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
    KingdomDecisionAddResult AddDecision(Kingdom kingdom, KingdomDecision kingdomDecision, bool ignoreInfluenceCost, float? randomFloat, bool applyInfluenceCost, bool? wasQueued);
    void RunAddDecision(Kingdom kingdom, KingdomDecision kingdomDecision, bool ignoreInfluenceCost, float randomFloat, bool? wasQueued);
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
            // A client only proposes, the same shape RemoveDecisionPrefix uses. The decision is applied
            // when the server broadcasts its answer back, otherwise the proposer holds an optimistic copy
            // that the broadcast then adds a second time and every later decision index is off by one.
            MessageBroker.Instance.Publish(kingdom,
                new DecisionAdded(kingdom, kingdomDecision, ignoreInfluenceCost, randomNumber: default, wasQueued: null));
            return false;
        }
        ApplyAndAnnounce(kingdom, kingdomDecision, ignoreInfluenceCost, randomFloat: null, wasQueued: null);
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
            decisionVoteManager.ClearDecisionState(kingdom, index);
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
    public KingdomDecisionAddResult AddDecision(
        Kingdom kingdom,
        KingdomDecision kingdomDecision,
        bool ignoreInfluenceCost,
        float? randomFloat,
        bool applyInfluenceCost,
        bool? wasQueued)
    {
        KingdomRegistry.EnsureRuntimeCollections(kingdom);
        if (applyInfluenceCost && !ignoreInfluenceCost)
        {
            Clan proposerClan = kingdomDecision.ProposerClan;
            int influenceCost = kingdomDecision.GetInfluenceCost(proposerClan);
            ChangeClanInfluenceAction.Apply(proposerClan, (float)(-(float)influenceCost));
        }
        // Take the server's answer when it sent one. Eligibility also filters on player connectivity on the
        // server, so re-deciding here offsets _unresolvedDecisions and every later index based message.
        bool queueDecision = wasQueued ?? decisionVoteManager.HasEligiblePlayerClan(kingdomDecision);
        CampaignEventDispatcher.Instance.OnKingdomDecisionAdded(kingdomDecision, queueDecision);
        if (!queueDecision)
        {
            // Only the server elects. On a client the answer means "this one never entered the queue",
            // because a second evaluation there re-applies outcome actions the server already replicated.
            if (ModInformation.IsClient) return new KingdomDecisionAddResult(default, false);

            CoopKingdomElection election = new CoopKingdomElection(kingdomDecision, randomFloat);
            election.StartElectionCoop();
            return new KingdomDecisionAddResult(election.RandomFloat, false);
        }
        kingdom._unresolvedDecisions.Add(kingdomDecision);
        decisionVoteManager.RegisterDecision(kingdomDecision);
        return new KingdomDecisionAddResult(default, true);
    }
    public void RunAddDecision(Kingdom kingdom, KingdomDecision kingdomDecision, bool ignoreInfluenceCost, float randomFloat, bool? wasQueued)
    {
        RunKingdomMutation(() =>
        {
            if (ModInformation.IsServer)
            {
                // This is a client's proposal being applied, and announcing the apply is what carries the
                // server's queue answer out to every client, the proposer included, in one broadcast.
                ApplyAndAnnounce(kingdom, kingdomDecision, ignoreInfluenceCost, randomFloat, wasQueued);
                return;
            }
            // Only the server charges influence, the client mirrors the cost the server replicates.
            AddDecision(kingdom, kingdomDecision, ignoreInfluenceCost, randomFloat, applyInfluenceCost: false, wasQueued);
        });
    }
    private void ApplyAndAnnounce(
        Kingdom kingdom,
        KingdomDecision kingdomDecision,
        bool ignoreInfluenceCost,
        float? randomFloat,
        bool? wasQueued)
    {
        var result = AddDecision(kingdom, kingdomDecision, ignoreInfluenceCost, randomFloat, applyInfluenceCost: true, wasQueued);
        MessageBroker.Instance.Publish(kingdom,
            new DecisionAdded(kingdom, kingdomDecision, ignoreInfluenceCost, result.RandomNumber, result.WasQueued));
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
/// <summary>
/// Outcome of a single <c>Kingdom.AddDecision</c> apply.
/// </summary>
public readonly struct KingdomDecisionAddResult
{
    public readonly float RandomNumber;
    /// <summary>
    /// True when the decision was queued in <c>Kingdom._unresolvedDecisions</c>, false when it was resolved in place.
    /// </summary>
    public readonly bool WasQueued;
    public KingdomDecisionAddResult(float randomNumber, bool wasQueued)
    {
        RandomNumber = randomNumber;
        WasQueued = wasQueued;
    }
}
