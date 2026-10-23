using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Kingdoms.Extentions;
using GameInterface.Services.Kingdoms.Messages;
using Serilog;
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
    private static readonly ILogger Logger = LogManager.GetLogger<KingdomInterface>();
    private readonly IKingdomDecisionVoteManager decisionVoteManager;
    private readonly IOfflineWarProtection offlineWarProtection;
    private readonly IKingdomDecisionDataConverter decisionDataConverter;
    public KingdomInterface(
        IKingdomDecisionVoteManager decisionVoteManager,
        IOfflineWarProtection offlineWarProtection,
        IKingdomDecisionDataConverter decisionDataConverter)
    {
        this.decisionVoteManager = decisionVoteManager;
        this.offlineWarProtection = offlineWarProtection;
        this.decisionDataConverter = decisionDataConverter;
    }
    public bool AddDecisionPrefix(Kingdom kingdom, KingdomDecision kingdomDecision, bool ignoreInfluenceCost)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient)
        {
            // The server could not add it for everyone, so a local copy would shift this client's indexes.
            if (!decisionDataConverter.IsSupported(kingdomDecision))
            {
                Logger.Warning("Dropped a {DecisionType} proposal: Coop cannot send this kingdom decision type",
                    kingdomDecision.GetType().Name);
                MessageBroker.Instance.Publish(this, new SendInformationMessage(
                    "This kingdom decision can't be proposed in Coop because its type isn't synced."));
                return false;
            }
            float clientRandomNumber = AddDecision(kingdom, kingdomDecision, ignoreInfluenceCost, applyInfluenceCost: false);
            MessageBroker.Instance.Publish(kingdom,
                new DecisionAdded(kingdom, kingdomDecision, ignoreInfluenceCost, clientRandomNumber));
            return false;
        }
        if (offlineWarProtection.ShouldRefuse(kingdomDecision, out IFaction target))
        {
            Logger.Information(
                "Refused {DecisionType} from AI clan {ProposerClanId} against {TargetId}: no player of the target is online",
                kingdomDecision.GetType().Name, kingdomDecision.ProposerClan.StringId, target.StringId);
            return false;
        }
        float randomNumber = AddDecision(kingdom, kingdomDecision, ignoreInfluenceCost, applyInfluenceCost: true);
        MessageBroker.Instance.Publish(kingdom,
            new DecisionAdded(kingdom, kingdomDecision, ignoreInfluenceCost, randomNumber));
        return false;
    }
    public bool RemoveDecisionPrefix(Kingdom kingdom, KingdomDecision kingdomDecision)
    {
        // The server removes by index, so a client's own removal (a replayed owner change runs the claimant
        // behavior under AllowedThread) would shift the list and make that remove drop the next decision.
        if (ModInformation.IsClient) return false;
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
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
        // Votes and removals address decisions by list index, so a type clients cannot receive never enters
        // the list; it resolves here like a decision no player votes on.
        bool hasEligiblePlayerClan = decisionDataConverter.IsSupported(kingdomDecision) &&
            decisionVoteManager.HasEligiblePlayerClan(kingdomDecision);
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
            kingdom._unresolvedDecisions.Remove(kingdomDecision);
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
