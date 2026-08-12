using Common.Messaging;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using Common;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Core.ViewModelCollection.Selector;

namespace GameInterface.Services.Clans.Patches;

[HarmonyPatch(typeof(ClanPartyItemVM))]
internal class ClanPartyItemVMPatches
{
    private sealed class CaravanViewModels
    {
        internal readonly List<WeakReference<ClanPartyItemVM>> Items = new();
    }

    private static readonly ConditionalWeakTable<MobileParty, CaravanViewModels>
        CaravanViews = new();

    [HarmonyPatch(MethodType.Constructor, new Type[]
    {
        typeof(PartyBase),
        typeof(Action<ClanPartyItemVM>),
        typeof(Action),
        typeof(Action),
        typeof(ClanPartyItemVM.ClanPartyType),
        typeof(IDisbandPartyCampaignBehavior),
        typeof(ITeleportationCampaignBehavior),
    })]
    [HarmonyPostfix]
    public static void ConstructorPostfix(ClanPartyItemVM __instance)
    {
        MobileParty party = __instance?.Party?.MobileParty;
        if (!ModInformation.IsClient || party?.IsCaravan != true)
            return;

        CaravanViewModels views = CaravanViews.GetOrCreateValue(party);
        lock (views.Items)
            views.Items.Add(new WeakReference<ClanPartyItemVM>(__instance));
        RefreshIncome(__instance, notifyParent: false);
    }

    [HarmonyPatch(nameof(ClanPartyItemVM.UpdateProperties))]
    [HarmonyPostfix]
    public static void UpdatePropertiesPostfix(ClanPartyItemVM __instance)
    {
        if (ModInformation.IsClient &&
            __instance?.Party?.MobileParty?.IsCaravan == true)
            RefreshIncome(__instance, notifyParent: true);
    }

    internal static void RefreshCaravanIncome(MobileParty caravan)
    {
        if (!ModInformation.IsClient || caravan == null ||
            !CaravanViews.TryGetValue(caravan, out CaravanViewModels views))
            return;

        lock (views.Items)
        {
            for (int i = views.Items.Count - 1; i >= 0; i--)
            {
                if (!views.Items[i].TryGetTarget(out ClanPartyItemVM viewModel))
                {
                    views.Items.RemoveAt(i);
                    continue;
                }
                RefreshIncome(viewModel, notifyParent: true);
            }
        }
    }

    private static void RefreshIncome(
        ClanPartyItemVM viewModel,
        bool notifyParent)
    {
        MobileParty caravan = viewModel?.Party?.MobileParty;
        if (Campaign.Current?.Models?.ClanFinanceModel == null ||
            caravan?.IsCaravan != true)
            return;

        int income = Campaign.Current.Models.ClanFinanceModel
            .CalculateOwnerIncomeFromCaravan(caravan);
        if (viewModel.Income == income)
            return;

        viewModel.Income = income;
        viewModel.OnPropertyChangedWithValue(
            income, nameof(ClanPartyItemVM.Income));
        if (notifyParent)
            viewModel._onExpenseChange?.Invoke();
    }

    [HarmonyPatch(nameof(ClanPartyItemVM.UpdatePartyBehaviorSelectionUpdate))]
    [HarmonyPrefix]
    public static bool UpdatePartyBehaviorSelectionUpdatePrefix(ref ClanPartyItemVM __instance, SelectorVM<SelectorItemVM> s)
    {
        if (s.SelectedIndex != (int)__instance.Party.MobileParty.Objective)
        {
            // Manage setting the party behavior on the server
            var message = new PartyBehaviorUpdatedOnSelection(__instance.Party.MobileParty, (MobileParty.PartyObjective)s.SelectedIndex);
            MessageBroker.Instance.Publish(__instance, message);
        }

        return false;
    }
    
    [HarmonyPatch(nameof(ClanPartyItemVM.OnAutoRecruitChanged))]
    [HarmonyPrefix]
    public static bool OnAutoRecruitChangedPrefix(ref ClanPartyItemVM __instance, bool value)
    {
        if (__instance.Party.IsMobile && __instance.Party.MobileParty.IsGarrison)
        {
            Settlement homeSettlement = __instance.Party.MobileParty.HomeSettlement;
            if (homeSettlement?.Town != null)
            {
                // Manage setting auto recruitment on the server
                var message = new AutoRecruitChangedForSettlement(__instance.Party.MobileParty.HomeSettlement, value);
                MessageBroker.Instance.Publish(__instance, message);
            }
        }

        return false;
    }

}
