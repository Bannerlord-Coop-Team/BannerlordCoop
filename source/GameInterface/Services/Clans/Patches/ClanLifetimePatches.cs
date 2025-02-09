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

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches for lifecycle of <see cref="Clan"/> objects.
/// </summary>
[HarmonyPatch]
internal class ClanLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroLifetimePatches>();

    /// <summary>
    /// Disables string id setting in <see cref="Clan.CreateClan(string)"/> so we can manage that in our patches
    /// </summary>
    [HarmonyPatch(typeof(Clan), nameof(Clan.CreateClan), new Type[] { typeof(string) })]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var set_stringId = AccessTools.PropertySetter(typeof(MBObjectBase), nameof(MBObjectBase.StringId));

        foreach (var instr in instructions)
        {
            if (instr.opcode == OpCodes.Callvirt && instr.operand as MethodInfo == set_stringId)
            {
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Pop);
            }
            else
            {
                yield return instr;
            }
        }
    }

    [HarmonyPatch(typeof(Clan), MethodType.Constructor)]
    [HarmonyPrefix]
    private static bool ctorPrefix(ref Clan __instance)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Clan), Environment.StackTrace);

            return true;
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            objectManager.AddNewObject(__instance, out var newId);

            var message = new ClanCreated(__instance);

            MessageBroker.Instance.Publish(null, message);
        }

        return true;
    }

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
