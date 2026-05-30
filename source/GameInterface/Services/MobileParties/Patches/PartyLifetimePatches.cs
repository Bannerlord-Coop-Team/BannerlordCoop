using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class PartyLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyLifetimePatches>();

    [HarmonyPatch(nameof(MobileParty.RemoveParty))]
    [HarmonyPrefix]
    private static bool RemoveParty_Prefix(ref MobileParty __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed unmanaged {name}", typeof(MobileParty));
            return false;
        }

        MessageBroker.Instance.Publish(__instance, new PartyDestroyed(__instance));

        return true;
    }
    [HarmonyPatch(nameof(MobileParty.CreateParty))]
    [HarmonyPostfix]
    private static void CreateParty_Postfix(ref MobileParty __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(__result, new PartyCreated(__result));
    }
}