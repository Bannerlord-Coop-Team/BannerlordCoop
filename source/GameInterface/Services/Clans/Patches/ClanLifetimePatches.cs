using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Clans.Messages.Lifetime;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// Patches for lifecycle of <see cref="Clan"/> objects.
/// </summary>
[HarmonyPatch]
internal class ClanLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanLifetimePatches>();

    [HarmonyPatch(typeof(DestroyClanAction), "ApplyInternal")]
    [HarmonyPrefix]
    static bool DestroyPrefix(Clan destroyedClan, int details)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Clan), Environment.StackTrace);
            return false;
        }

        MessageBroker.Instance.Publish(destroyedClan, new ClanDestroyed(destroyedClan, details));

        return true;
    }
}
