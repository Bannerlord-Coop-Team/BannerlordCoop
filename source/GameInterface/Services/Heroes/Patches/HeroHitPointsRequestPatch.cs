using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches;

/// <summary>
/// <see cref="Hero.HitPoints"/> is a server-authoritative synced property, but a client downs its own hero
/// locally in its mission (<c>Mission.OnAgentRemoved</c> → <c>Hero.set_HitPoints</c>) and that change never
/// reached the server — leaving the hero's health/wounded state desynced (low on the client, full on the
/// server). This forwards a client's HitPoints change for the hero it controls up to the server, which
/// applies it authoritatively; the HitPoints property sync then replicates the value back to everyone.
/// </summary>
[HarmonyPatch(typeof(Hero))]
internal class HeroHitPointsRequestPatch
{
    [HarmonyPatch(nameof(Hero.HitPoints), MethodType.Setter)]
    [HarmonyPrefix]
    private static void Prefix(Hero __instance, int value)
    {
        // The server is authoritative; only clients request changes.
        if (ModInformation.IsServer) return;

        // Skip server-approved applies (the property-sync receive path runs under the original-call policy).
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        // Only forward changes for the hero this client actually controls (its own mission casualties).
        if (!__instance.IsControlledByThisInstance()) return;

        // Nothing to request if the value is unchanged.
        if (__instance.HitPoints == value) return;

        MessageBroker.Instance.Publish(__instance, new HeroHitPointsChangeRequested(__instance, value));
    }
}
