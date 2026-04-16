using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.SiegeEngineMissiles.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEngineMissiles.Patches;

/// <summary>
/// Patches for managing lifetime of <see cref="SiegeEngineMissile"/> objects.
/// </summary>
[HarmonyPatch]
internal class SiegeEngineMissileLifetimePatches
{
    static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineMissileLifetimePatches>();

    static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(SiegeEvent.SiegeEngineMissile));

    static bool Prefix(ref SiegeEvent.SiegeEngineMissile __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(SiegeEvent.SiegeEngineMissile));
            return true;
        }

        var message = new SiegeEngineMissileCreated(__instance);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }
}