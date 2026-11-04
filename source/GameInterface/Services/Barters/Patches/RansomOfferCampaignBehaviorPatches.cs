using Common;
using Common.Messaging;
using GameInterface.Services.Barters.Interfaces;
using GameInterface.Services.Barters.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Barters.Patches;

[HarmonyPatch(typeof(RansomOfferCampaignBehavior))]
internal class RansomOfferCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(RansomOfferCampaignBehavior.ConsiderRansomPrisoner))]
    [HarmonyPrefix]
    public static bool ConsiderRansomPrisonerPrefix(RansomOfferCampaignBehavior __instance, Hero hero)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IRansomOfferCampaignBehaviorInterface>(out var ransomOfferCampaignBehaviorInterface)) return false;

        ransomOfferCampaignBehaviorInterface.ConsiderRansomPrisoner(hero);

        return false;
    }

    [HarmonyPatch(nameof(RansomOfferCampaignBehavior.HandleDeclineRansomOffer))]
    [HarmonyPrefix]
    public static bool HandleDeclineRansomOfferPrefix(RansomOfferCampaignBehavior __instance, Hero victim)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IRansomOfferCampaignBehaviorInterface>(out var ransomOfferCampaignBehaviorInterface)) return false;

        ransomOfferCampaignBehaviorInterface.HandleDeclineRansomOffer(victim);

        return false;
    }

    [HarmonyPatch(nameof(RansomOfferCampaignBehavior.OnRansomOffered))]
    [HarmonyPrefix]
    public static void OnRansomOfferedPrefix(RansomOfferCampaignBehavior __instance, Hero captiveHero)
    {
        // Track the offer the open inquiry is for, so the response can't apply to a newer offer the server sent meanwhile
        __instance._currentRansomHero = captiveHero;
    }

    [HarmonyPatch(nameof(RansomOfferCampaignBehavior.OnRansomOffered))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> OnRansomOfferedTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            // Swap the locally calculated price for the one the server sent with the offer
            if (instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo field && field.Name == "ransomPrice")
            {
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RansomOfferCampaignBehaviorPatches), nameof(GetClientRansomPrice)));
            }

            yield return instruction;
        }
    }

    private static int GetClientRansomPrice()
    {
        if (!ContainerProvider.TryResolve<IRansomOfferCampaignBehaviorInterface>(out var ransomOfferCampaignBehaviorInterface)) return 0;

        return ransomOfferCampaignBehaviorInterface.GetClientRansomPrice();
    }

    [HarmonyPatch(nameof(RansomOfferCampaignBehavior.AcceptRansomOffer))]
    [HarmonyPrefix]
    public static bool AcceptRansomOfferPrefix(RansomOfferCampaignBehavior __instance, int ransomPrice)
    {
        MessageBroker.Instance.Publish(__instance, new RansomOfferAccepted(Hero.MainHero, __instance._currentRansomHero));

        return false;
    }

    [HarmonyPatch(nameof(RansomOfferCampaignBehavior.DeclineRansomOffer))]
    [HarmonyPrefix]
    public static bool DeclineRansomOfferPrefix(RansomOfferCampaignBehavior __instance)
    {
        // Server path uses RansomOfferCampaignBehaviorInterface.DeclineRansomOffer directly instead.
        MessageBroker.Instance.Publish(__instance, new RansomOfferDeclined(Hero.MainHero, __instance._currentRansomHero));

        return false;
    }

    [HarmonyPatch(nameof(RansomOfferCampaignBehavior.OnPrisonersChangeInSettlement))]
    [HarmonyPrefix]
    public static bool OnPrisonersChangeInSettlementPrefix(RansomOfferCampaignBehavior __instance, FlattenedTroopRoster roster, Hero prisoner, bool takenFromDungeon)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IRansomOfferCampaignBehaviorInterface>(out var ransomOfferCampaignBehaviorInterface)) return false;

        ransomOfferCampaignBehaviorInterface.OnPrisonersChangeInSettlement(roster, prisoner, takenFromDungeon);

        return false;
    }

    [HarmonyPatch(nameof(RansomOfferCampaignBehavior.HourlyTick))]
    [HarmonyPrefix]
    public static bool HourlyTickPrefix(RansomOfferCampaignBehavior __instance)
    {
        if (ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IRansomOfferCampaignBehaviorInterface>(out var ransomOfferCampaignBehaviorInterface)) return false;

        ransomOfferCampaignBehaviorInterface.HourlyTick();

        return false;
    }
}
