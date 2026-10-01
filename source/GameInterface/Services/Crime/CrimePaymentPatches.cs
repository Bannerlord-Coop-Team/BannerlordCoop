using Common;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Crime;

[HarmonyPatch(typeof(PayForCrimeAction), nameof(PayForCrimeAction.ApplyInternal))]
internal class CrimePaymentActionPatch
{
    private static bool Prefix(IFaction faction, CrimeModel.PaymentMethod paymentMethod)
    {
        if (ModInformation.IsServer) return ResolvedMainHeroContext.ResolvedMainHero != null;
        if (ContainerProvider.TryResolve<ICrimeRatingService>(out var ratings))
            ratings.RequestPayment(faction, paymentMethod);
        return false;
    }
}

[HarmonyPatch]
internal class CrimePaymentMenuPatch
{
    private static readonly Dictionary<string, CrimeModel.PaymentMethod> Methods = new()
    {
        [nameof(CrimeCampaignBehavior.criminal_inside_menu_give_money_on_consequence)] = CrimeModel.PaymentMethod.Gold,
        [nameof(CrimeCampaignBehavior.criminal_inside_menu_give_influence_on_consequence)] = CrimeModel.PaymentMethod.Influence,
        [nameof(CrimeCampaignBehavior.criminal_inside_menu_pay_by_punishment_on_consequence)] = CrimeModel.PaymentMethod.Punishment,
        [nameof(CrimeCampaignBehavior.criminal_inside_menu_give_punishment_and_money_on_consequence)] = CrimeModel.PaymentMethod.Gold | CrimeModel.PaymentMethod.Punishment,
        [nameof(CrimeCampaignBehavior.criminal_inside_menu_give_your_life_on_consequence)] = CrimeModel.PaymentMethod.Execution,
    };

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in Methods.Keys) yield return AccessTools.Method(typeof(CrimeCampaignBehavior), name);
    }

    private static bool Prefix(MethodBase __originalMethod)
    {
        if (ModInformation.IsClient)
            PayForCrimeAction.Apply(Settlement.CurrentSettlement.MapFaction, Methods[__originalMethod.Name]);
        // The authoritative result advances the menu after the payment succeeds.
        return false;
    }
}
