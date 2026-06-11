using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PlayerCaptivityService.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

[HarmonyPatch(typeof(Campaign))]
internal class CampaignTickPatch
{
    [HarmonyPatch(nameof(Campaign.Tick))]
    [HarmonyPostfix]
    private static void Postfix_Tick()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        MessageBroker.Instance.Publish(Campaign.Current, new CampaignTick());
    }
}
