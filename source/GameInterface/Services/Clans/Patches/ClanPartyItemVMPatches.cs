using Common.Messaging;
using GameInterface.Services.Clans.Data;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanPartyItem;
using TaleWorlds.Core;

namespace GameInterface.Services.Clans.Patches;

/// <summary>
/// v1.5 made <see cref="ClanPartyItemVM"/> abstract: a row is either an existing party
/// (<see cref="ClanPartyItemWithPartyVM"/>) or a hero whose party is still to be created
/// (<see cref="ClanPartyItemWithHeroVM"/>), and each implements the patched members.
/// </summary>
[HarmonyPatch]
internal class ClanPartyItemVMUpdatePropertiesPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ClanPartyItemWithPartyVM), nameof(ClanPartyItemWithPartyVM.UpdateProperties));
        yield return AccessTools.Method(typeof(ClanPartyItemWithHeroVM), nameof(ClanPartyItemWithHeroVM.UpdateProperties));
    }

    [HarmonyPostfix]
    public static void UpdatePropertiesPostfix(ClanPartyItemVM __instance)
    {
        var party = __instance.Party?.MobileParty;
        bool canManage = CoopClanPermissions.CanManageParty(party);
        bool canAssignRoles = CoopClanPermissions.CanAssignRoles(party);
        bool isPlayerParty = party?.IsPlayerParty() == true;
        if (isPlayerParty) __instance.ShouldPartyHaveExpense = false;
        var managementHint = GameTexts.FindText(isPlayerParty
            ? "str_coop_clan_player_party_protected" : "str_coop_clan_party_leader_only");

        __instance.IsChangeLeaderVisible &= canManage;
        __instance.IsChangeLeaderEnabled &= canManage;
        if (!canManage) __instance.ChangeLeaderHint.HintText = managementHint;
        // v1.5 replaced the party behavior selector with per-leader command toggles.
        __instance.AreCommandControlsVisible &= canManage;
        __instance.AreNavalControlsVisible &= canManage;
        __instance.CanUseActions &= canManage || canAssignRoles;
        if (!canManage && !canAssignRoles)
            __instance.ActionsDisabledHint.HintText = managementHint;

        if (__instance.ExpenseItem != null && !canManage)
        {
            __instance.ExpenseItem.IsEnabled = false;
            __instance.ExpenseItem.WageLimitHint.HintText = GameTexts.FindText(isPlayerParty
                ? "str_coop_clan_player_party_wages_disabled" : "str_coop_clan_party_leader_only");
            __instance.ActionsDisabledHint.HintText = __instance.ExpenseItem.WageLimitHint.HintText;
        }
        if (!canManage && __instance.AutoRecruitmentHint != null)
            __instance.AutoRecruitmentHint.HintText = managementHint;
        foreach (var role in __instance.Roles)
        {
            if (!canAssignRoles)
                role.SetEnabled(false, GameTexts.FindText("str_coop_clan_party_roles_owner_only"));
        }
    }
}

[HarmonyPatch]
internal class ClanPartyItemVMExecuteChangeLeaderPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(ClanPartyItemWithPartyVM), nameof(ClanPartyItemWithPartyVM.ExecuteChangeLeader));
        yield return AccessTools.Method(typeof(ClanPartyItemWithHeroVM), nameof(ClanPartyItemWithHeroVM.ExecuteChangeLeader));
    }

    [HarmonyPrefix]
    public static bool ExecuteChangeLeaderPrefix(ClanPartyItemVM __instance)
    {
        return CoopClanPermissions.CanManageParty(__instance.Party?.MobileParty);
    }
}

/// <summary>
/// The command toggles write the party leader's <see cref="Hero"/> configuration. The server owns it,
/// so a toggle on a client becomes a request and the value comes back through the hero's AutoSync.
/// </summary>
[HarmonyPatch(typeof(ClanPartyItemVM))]
internal class ClanPartyItemVMPatches
{
    [HarmonyPatch(nameof(ClanPartyItemVM.OnMayJoinOtherArmiesChanged))]
    [HarmonyPrefix]
    public static bool OnMayJoinOtherArmiesChangedPrefix(ClanPartyItemVM __instance, bool value)
        => RequestPartyConfiguration(__instance, PartyConfigurationFlag.CanJoinArmy, value);

    [HarmonyPatch(nameof(ClanPartyItemVM.OnAllowRaidingChanged))]
    [HarmonyPrefix]
    public static bool OnAllowRaidingChangedPrefix(ClanPartyItemVM __instance, bool value)
        => RequestPartyConfiguration(__instance, PartyConfigurationFlag.CanRaid, value);

    [HarmonyPatch(nameof(ClanPartyItemVM.OnDonateTroopsToGarrisonsChanged))]
    [HarmonyPrefix]
    public static bool OnDonateTroopsToGarrisonsChangedPrefix(ClanPartyItemVM __instance, bool value)
        => RequestPartyConfiguration(__instance, PartyConfigurationFlag.CanDonateTroopsToGarrison, value);

    [HarmonyPatch(nameof(ClanPartyItemVM.OnHasFleetChanged))]
    [HarmonyPrefix]
    public static bool OnHasFleetChangedPrefix(ClanPartyItemVM __instance, bool value)
        => RequestPartyConfiguration(__instance, PartyConfigurationFlag.CanHaveFleet, value);

    private static bool RequestPartyConfiguration(ClanPartyItemVM instance, PartyConfigurationFlag flag, bool value)
    {
        Hero leader = instance.Leader;
        if (leader == null) return false;
        if (!CoopClanPermissions.CanManageParty(instance.Party?.MobileParty)) return false;
        if (PartyConfigurationFlags.Get(leader, flag) == value) return false;

        MessageBroker.Instance.Publish(instance, new PartyConfigurationChangedOnSelection(leader, flag, value));
        return false;
    }
}

[HarmonyPatch(typeof(ClanPartyItemWithPartyVM))]
internal class ClanPartyItemWithPartyVMPatches
{
    [HarmonyPatch(nameof(ClanPartyItemWithPartyVM.OnAutoRecruitChanged))]
    [HarmonyPrefix]
    public static bool OnAutoRecruitChangedPrefix(ClanPartyItemWithPartyVM __instance, bool value)
    {
        if (!CoopClanPermissions.CanManageParty(__instance.Party?.MobileParty)) return false;

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
