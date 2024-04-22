using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
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
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Patches for lifecycle of <see cref="MobileParty"/> objects.
/// </summary>
// TODO re-enable when working
// [HarmonyPatch(typeof(MobileParty))]
internal class PartyLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroLifetimePatches>();

    [HarmonyPatch(typeof(MobileParty), MethodType.Constructor)]
    private static bool Prefix(ref MobileParty __instance)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // This is needed to prevent Party.CreateParty from crashing
        __instance.StringId = "";

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MobileParty), Environment.StackTrace);
            return true;
        }

        // Allow method if container is not setup
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return true;
        if (ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) == false) return true;
        if (ContainerProvider.TryResolve<INetworkConfiguration>(out var configuration) == false) return true;

        if (objectManager.AddNewObject(__instance, out var stringID) == false) return true;

        var data = new PartyCreationData(__instance);
        var message = new PartyCreated(data);

        using (new MessageTransaction<NewPartySynced>(messageBroker, configuration.ObjectCreationTimeout))
        {
            MessageBroker.Instance.Publish(__instance, message);
        }

        return true;
    }


    private static readonly ConstructorInfo MobileParty_ctor = AccessTools.Constructor(typeof(MobileParty));
    public static void OverrideCreateNewParty(string partyId)
    {
        MobileParty newParty = ObjectHelper.SkipConstructor<MobileParty>();

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return;

        if (objectManager.AddExisting(partyId, newParty) == false) return;

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                MobileParty_ctor.Invoke(newParty, Array.Empty<object>());
            }
        });

        var data = new PartyCreationData(newParty);
        var message = new PartyCreated(data);
        MessageBroker.Instance.Publish(newParty, message);
    }


    [HarmonyPatch(nameof(MobileParty.RemoveParty))]
    [HarmonyPrefix]
    private static bool RemoveParty_Prefix(ref MobileParty __instance)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client destroyed unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(MobileParty), Environment.StackTrace);
            return true;
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return true;

        // Clean up object manager
        objectManager.Remove(__instance);

        var data = new PartyDestructionData(__instance);
        var message = new PartyDestroyed(data);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    public static void OverrideRemoveParty(string partyId)
    {
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return;

        if (objectManager.TryGetObject<MobileParty>(partyId, out var party) == false) return;

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                party.RemoveParty();
            }
        });

        var data = new PartyDestructionData(party);
        var message = new PartyDestroyed(data);

        MessageBroker.Instance.Publish(party, message);
    }

    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.CreateParty))]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var set_stringId = AccessTools.PropertySetter(typeof(MBObjectBase), nameof(MBObjectBase.StringId));

        foreach(var instr in instructions)
        {
            if (instr.opcode == OpCodes.Callvirt && instr.operand as MethodInfo == set_stringId)
            {
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PartyLifetimePatches), "SetStringIdIntercept"));
                continue;
            }

            yield return instr;
        }
    }

    private static void SetStringIdIntercept(MobileParty instance, string value)
    {
        if (instance.StringId == null)
        {
            instance.StringId = value;
        }
    }
}
