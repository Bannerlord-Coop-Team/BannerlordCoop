using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.PartyComponents;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(MobileParty))]
internal class MobilePartyDataPatches
{

    private static readonly ILogger Logger = LogManager.GetLogger<HeroFieldPatches>();

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(MobileParty), nameof(MobileParty.CreateParty));
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PartyComponentTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var partyComponentField = AccessTools.Field(typeof(MobileParty), nameof(MobileParty._partyComponent));
        var fieldIntercept = AccessTools.Method(typeof(MobilePartyDataPatches), nameof(SetPartyComponentIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(partyComponentField))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    private static void SetPartyComponentIntercept(MobileParty instance, PartyComponent value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._partyComponent = value;
            return;
        }
        if (ModInformation.IsClient)
        {
            Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
            instance._partyComponent = value;
            return;
        }

        if (ContainerProvider.TryResolve(out PartyComponentRegistry partyComponentRegistry) == false) return;

        if (partyComponentRegistry.TryGetId(value, out string componentId) == false)
        {
            Logger.Error("Component was not registered with PartyComponentRegistry");
            return;
        }

        MessageBroker.Instance.Publish(instance, new PartyComponentChanged(instance.StringId, componentId));

        instance._partyComponent = value;
    }
}
