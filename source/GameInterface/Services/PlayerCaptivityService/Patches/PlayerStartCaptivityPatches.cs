using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

[HarmonyPatch]
internal class PlayerStartCaptivityPatches
{
    // Capture the previous captor before the setter overwrites it. In the postfix the auto-property already
    // holds the new value, so the "did not change" check must compare against this snapshot, not the getter.
    [HarmonyPatch(typeof(Hero), nameof(Hero.PartyBelongedToAsPrisoner), MethodType.Setter)]
    [HarmonyPrefix]
    private static void Prefix_PartyBelongedToAsPrisoner(Hero __instance, out PartyBase __state)
    {
        __state = __instance.PartyBelongedToAsPrisoner;
    }

    [HarmonyPatch(typeof(Hero), nameof(Hero.PartyBelongedToAsPrisoner), MethodType.Setter)]
    [HarmonyPostfix]
    private static void Postfix_PartyBelongedToAsPrisoner(Hero __instance, PartyBase value, PartyBase __state)
    {
        if (ModInformation.IsServer) return;

        // Did not change
        if (__state == value) return;

        // Skip if not main hero
        if (!__instance.IsControlledByThisInstance()) return;

        var message = new PlayerCaptivityChanged(value);
        MessageBroker.Instance.Publish(null, message);
    }
}
