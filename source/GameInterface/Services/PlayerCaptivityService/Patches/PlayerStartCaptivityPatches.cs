using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

[HarmonyPatch]
internal class PlayerStartCaptivityPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerStartCaptivityPatches>();

    [HarmonyPatch(typeof(Hero), nameof(Hero.PartyBelongedToAsPrisoner), MethodType.Setter)]
    [HarmonyPostfix]
    private static void Postfix_PartyBelongedToAsPrisoner(Hero __instance, PartyBase value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (ModInformation.IsServer) return;

        // Did not change
        if (__instance.PartyBelongedToAsPrisoner == value) return;

        // Skip if not main hero
        if (__instance != Hero.MainHero) return;

        var message = new PlayerCaptivityChanged(value);
        MessageBroker.Instance.Publish(null, message);
    }
}
