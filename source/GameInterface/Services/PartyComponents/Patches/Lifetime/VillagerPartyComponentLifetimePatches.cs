﻿using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Patches.Lifetime;


/// <summary>
/// Harmony patches for the lifetime of a <see cref="VillagerPartyComponent"/> object
/// </summary>
[HarmonyPatch]
internal class VillagerPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillagerPartyComponentLifetimePatches>();

    private static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(VillagerPartyComponent));

    private static bool Prefix(VillagerPartyComponent __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(VillagerPartyComponent), Environment.StackTrace);
            return true;
        }

        var message = new PartyComponentCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}