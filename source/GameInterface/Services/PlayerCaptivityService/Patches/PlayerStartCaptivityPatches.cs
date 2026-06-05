using Common;
using Common.Messaging;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

[HarmonyPatch(typeof(Hero))]
internal class PlayerStartCaptivityPatches
{
    [HarmonyPatch(nameof(Hero.PartyBelongedToAsPrisoner), MethodType.Setter)]
    [HarmonyPostfix]
    private static void Postfix_PartyBelongedToAsPrisoner(Hero __instance, PartyBase value)
    {
        if (ModInformation.IsServer) return;

        // Did not change
        if (__instance.PartyBelongedToAsPrisoner == value) return;

        // Skip if not main hero
        if (__instance != Hero.MainHero) return;

        var message = new PlayerCaptivityChanged(value);
        MessageBroker.Instance.Publish(null, message);
    }
}
