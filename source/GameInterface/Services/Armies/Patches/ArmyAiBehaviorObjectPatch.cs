using Common;
using Common.Messaging;
using GameInterface.Policies;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using GameInterface.Services.Armies.Messages;

namespace GameInterface.Services.Armies.Patches;

[HarmonyPatch(typeof(Army), "set_AiBehaviorObject")]
internal class ArmyAiBehaviorObjectPatch
{
    [HarmonyPrefix]
    static bool Prefix(Army __instance, IMapPoint value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (ModInformation.IsClient) return false;

        var message = new ArmyAiBehaviorObjectChanged(__instance, value);
        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}