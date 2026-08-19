using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>Samples the local player army leader after campaign-map movement finishes.</summary>
[HarmonyPatch(typeof(CampaignTickCacheDataStore), nameof(CampaignTickCacheDataStore.RealTick))]
internal static class ArmyLeaderPositionConvergencePatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowed()) return;

        // RealTick has finished parallel movement, so the observed position belongs to this game-thread frame.
        MobileParty leaderParty = MobileParty.MainParty;
        if (leaderParty?.IsControlledByThisInstance() != true) return;
        if (leaderParty.Army?.LeaderParty != leaderParty) return;

        MessageBroker.Instance.Publish(
            leaderParty,
            new ArmyLeaderPositionObserved(leaderParty));
    }
}
