using Common;
using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;


[HarmonyPatch(typeof(RebellionsCampaignBehavior))]
internal class RebellionsCampaignBehaviorPatches
{
    private static MethodInfo CreateSettlementRebelClanMethod => AccessTools.Method(
        typeof(Clan),
        nameof(Clan.CreateSettlementRebelClan),
        new[] { typeof(Settlement), typeof(Hero), typeof(int) });
    private static MethodInfo IsNobleSetter => AccessTools.PropertySetter(typeof(Clan), nameof(Clan.IsNoble));
    private static MethodInfo PublishRebelClanIsNobleMethod => AccessTools.Method(typeof(RebellionsCampaignBehaviorPatches), nameof(PublishRebelClanIsNoble));

    [HarmonyPatch(nameof(RebellionsCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(RebellionsCampaignBehavior.CreateRebelPartyAndClan))]
    [HarmonyTranspiler]
    internal static IEnumerable<CodeInstruction> CreateRebelPartyAndClanTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var instructionList = new List<CodeInstruction>(instructions);
        int factoryCalls = 0;
        int factoryIndex = -1;
        int isNobleSetters = 0;
        int isNobleSetterIndex = -1;

        for (int index = 0; index < instructionList.Count; index++)
        {
            if (instructionList[index].Calls(CreateSettlementRebelClanMethod))
            {
                factoryCalls++;
                factoryIndex = index;
            }

            if (instructionList[index].Calls(IsNobleSetter))
            {
                isNobleSetters++;
                isNobleSetterIndex = index;
            }
        }

        if (factoryCalls != 1 || isNobleSetters != 1 || isNobleSetterIndex <= factoryIndex)
        {
            throw new InvalidOperationException(
                $"Failed to patch rebel clan IsNoble snapshot: found {factoryCalls} factory calls and {isNobleSetters} later setters.");
        }

        instructionList[isNobleSetterIndex].opcode = OpCodes.Call;
        instructionList[isNobleSetterIndex].operand = PublishRebelClanIsNobleMethod;
        return instructionList;
    }

    internal static void PublishRebelClanIsNoble(Clan clan, bool isNoble)
    {
        clan.IsNoble = isNoble;

        if (ModInformation.IsServer)
        {
            MessageBroker.Instance.Publish(clan, new SettlementRebelClanInitialized(clan));
        }
    }
}
