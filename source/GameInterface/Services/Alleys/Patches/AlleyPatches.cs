using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Alleys.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Alleys.Patches;

/// <summary>
/// Routes authoritative <see cref="Alley"/> mutations through the network.
/// An alley's only persisted state is its owner; <c>State</c> and the owner's
/// <c>OwnedAlleys</c> list are both maintained by <c>Alley.SetOwner</c>, so the
/// owner change is replicated by replaying <c>SetOwner</c> on every client.
/// </summary>
[HarmonyPatch(typeof(Alley))]
internal class AlleyPatches
{
    [HarmonyPatch(nameof(Alley.SetOwner))]
    [HarmonyPrefix]
    private static bool SetOwnerPrefix(Alley __instance, Hero newOwner)
    {
        // Receive/apply path (ChangeAlleyOwner handler) runs the original under AllowedThread.
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Owner changes are server authoritative; a client never originates them.
        if (ModInformation.IsClient) return false;

        // Idempotent: nothing to replicate if the owner is unchanged.
        if (__instance.Owner == newOwner) return false;

        MessageBroker.Instance.Publish(__instance, new AlleyOwnerChanged(__instance, newOwner));

        // Run the real SetOwner on the server with patches live so the change replicates.
        return true;
    }
}
