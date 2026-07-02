using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Patches;

[HarmonyPatch(typeof(VillagerPartyComponent))]
internal class VillagerPartyComponentPatches
{
}

[HarmonyPatch(typeof(VillagerPartyComponent))]
public class VillagerPartyComponentTranspilers
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillagerPartyComponentTranspilers>();

    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var ctor in AccessTools.GetDeclaredConstructors(typeof(VillagerPartyComponent)))
            yield return ctor;
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var villageSetter = AccessTools.PropertySetter(typeof(VillagerPartyComponent), nameof(VillagerPartyComponent.Village));
        var villageIntercept = AccessTools.Method(typeof(VillagerPartyComponentTranspilers), nameof(VillageSetIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(villageSetter))
            {
                yield return new CodeInstruction(OpCodes.Call, villageIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void VillageSetIntercept(VillagerPartyComponent instance, Village village)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance.Village = village;
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client updated managed {type}", "VillagerPartyComponent.Village");
            instance.Village = village;
            return;
        }

        MessageBroker.Instance.Publish(instance, new VillagerPartyVillageChanged(instance, village));

        instance.Village = village;
    }
}