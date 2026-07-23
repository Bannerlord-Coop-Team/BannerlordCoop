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
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.PartyComponents.Patches;

[HarmonyPatch(typeof(PartyComponent))]
internal class PartyComponentPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyComponentPatches>();

    [HarmonyPatch(nameof(PartyComponent.MobileParty), MethodType.Setter)]
    [HarmonyPrefix]
    private static void PrefixMobileParty(PartyComponent __instance, MobileParty value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
            return;
        
        if (ModInformation.IsClient)
        {
            Logger.Error("Client called managed PartyComponent.MobileParty setter");
            return;
        }

        var message = new PartyComponentMobilePartyUpdated(__instance, value);
        MessageBroker.Instance.Publish(__instance, message);
    }
}

[HarmonyPatch(typeof(PartyComponent))]
public class PartyComponentTranspilers
{
    private static readonly ILogger Logger = LogManager.GetLogger<PartyComponentTranspilers>();

    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PartyComponent), nameof(PartyComponent.ChangePartyLeader));
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var onChangeMethod = AccessTools.Method(typeof(PartyComponent), nameof(PartyComponent.OnChangePartyLeader));
        var onChangeIntercept = AccessTools.Method(typeof(PartyComponentTranspilers), nameof(OnChangePartyLeaderIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(onChangeMethod))
            {
                yield return new CodeInstruction(OpCodes.Call, onChangeIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void OnChangePartyLeaderIntercept(PartyComponent instance, Hero newLeader)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance.OnChangePartyLeader(newLeader);
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client ran managed {type}", "PartyComponent.OnChangePartyLeader");
            instance.OnChangePartyLeader(newLeader);
            return;
        }

        MessageBroker.Instance.Publish(instance, new PartyComponentLeaderChanged(instance, newLeader));

        instance.OnChangePartyLeader(newLeader);
    }
}