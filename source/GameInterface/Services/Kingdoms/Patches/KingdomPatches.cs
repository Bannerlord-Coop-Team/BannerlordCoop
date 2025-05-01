using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Extentions;
using GameInterface.Policies;
using GameInterface.Services.Kingdoms.Extentions;
using GameInterface.Services.Kingdoms.Messages;
using HarmonyLib;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;

namespace GameInterface.Services.Kingdoms.Patches;

/// <summary>
/// Disables functionality of policies in game.
/// </summary>
/// <seealso cref="PolicyObject"/>
[HarmonyPatch(typeof(Kingdom))]
internal class KingdomPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<KingdomPatches>();

    [HarmonyPatch(nameof(Kingdom.AddDecision))]
    [HarmonyPrefix]
    public static bool AddDecisionPrefix(Kingdom __instance, KingdomDecision kingdomDecision, bool ignoreInfluenceCost)
    {
        if (AllowedThread.IsThisThreadAllowed())
        {
            ModifiedAddDecision(__instance, kingdomDecision, ignoreInfluenceCost);
            return false;
        }

        if (CallPolicy.IsOriginalAllowed()) return true;

        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

        float randomNumber = ModifiedAddDecision(__instance, kingdomDecision, ignoreInfluenceCost);
        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
        messageBroker?.Publish(__instance,
            new DecisionAdded(__instance.StringId, kingdomDecision.ToKingdomDecisionData(), ignoreInfluenceCost, randomNumber));
        return false;
    }

    public static void RunCoopAddDecision(Kingdom kingdom, KingdomDecision kingdomDecision, bool ignoreInfluenceCost, float randomFloat)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            ModifiedAddDecision(kingdom, kingdomDecision, ignoreInfluenceCost, randomFloat);
        }, true); 
    }

    private static float ModifiedAddDecision(Kingdom __instance, KingdomDecision kingdomDecision, bool ignoreInfluenceCost, float? randomFloat = null)
    {
        if (!ignoreInfluenceCost)
        {
            Clan proposerClan = kingdomDecision.ProposerClan;
            int influenceCost = kingdomDecision.GetInfluenceCost(proposerClan);
            ChangeClanInfluenceAction.Apply(proposerClan, (float)(-(float)influenceCost));
        }
        bool flag;
        if (!kingdomDecision.DetermineChooser().Leader.IsHumanPlayerCharacter)
        {
            flag = kingdomDecision.DetermineSupporters().Any((Supporter x) => x.IsPlayer);
        }
        else
        {
            flag = true;
        }

        bool isPlayerInvolved = flag;
        CampaignEventDispatcher.Instance.OnKingdomDecisionAdded(kingdomDecision, isPlayerInvolved);

        var playerParties = Campaign.Current.CampaignObjectManager.GetPlayerMobileParties();
        if (playerParties.All(party => party.ActualClan.Kingdom != kingdomDecision.Kingdom))
        {
            CoopKingdomElection election = new CoopKingdomElection(kingdomDecision, randomFloat);
            election.StartElectionCoop();
            return election.RandomFloat;
        }

        __instance._unresolvedDecisions.Add(kingdomDecision);
        return default;
    }

    [HarmonyPatch(nameof(Kingdom.RemoveDecision))]
    [HarmonyPrefix]
    public static bool RemoveDecisionPrefix(Kingdom __instance, KingdomDecision kingdomDecision)
    {
        if (CallPolicy.IsOriginalAllowed()) return true;

        if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

        var index = __instance._unresolvedDecisions.FindIndex(decision => decision == kingdomDecision);

        ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
messageBroker?.Publish(__instance,
            new DecisionRemoved(__instance.StringId, index));

        return true;
    }

    public static void RunOriginalRemoveDecision(Kingdom kingdom, KingdomDecision kingdomDecision)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                kingdom.RemoveDecision(kingdomDecision);
            }
        }, true);
    }

    [HarmonyPatch("AddPolicy")]
    [HarmonyPrefix]
    public static bool AddPolicyPrefix()
    {
        return false;
    }

    [HarmonyPatch("RemovePolicy")]
    [HarmonyPrefix]
    public static bool RemovePolicyPrefix()
    {
        return false;
    }
}
