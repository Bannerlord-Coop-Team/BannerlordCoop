using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace GameInterface.Services.Armies.Patches;

/// <summary>
/// Describes whether an attached player's army siege graph is absent, incomplete, or ready.
/// </summary>
internal enum AttachedArmySiegeState
{
    None,
    Incomplete,
    Ready,
}

/// <summary>
/// Recovers client army-wait siege transitions and relays player leave or abandon actions.
/// </summary>
[HarmonyPatch]
internal class PlayerArmyWaitBehaviorPatches
{
    [HarmonyPatch(typeof(PlayerArmyWaitBehavior), nameof(PlayerArmyWaitBehavior.OnTick))]
    [HarmonyPrefix]
    private static bool OnTickPrefix()
    {
        if (ModInformation.IsClient)
        {
            TryStartAttachedArmySiege();
        }

        return false;
    }

    [HarmonyPatch(typeof(PlayerArmyWaitBehavior), "ArmyWaitMenuTick")]
    [HarmonyPrefix]
    private static bool ArmyWaitMenuTickPrefix(MenuCallbackArgs args)
    {
        if (ModInformation.IsServer) return false;

        var mainParty = MobileParty.MainParty;
        var siegeState = GetAttachedArmySiegeState(mainParty, out var settlement);
        if (siegeState == AttachedArmySiegeState.Ready)
        {
            StartAttachedArmySiege(settlement);
            return false;
        }

        if (siegeState == AttachedArmySiegeState.Incomplete)
            return false;

        var encounterGameMenuModel = Campaign.Current?.Models?.EncounterGameMenuModel;
        if (encounterGameMenuModel == null)
            return false;

        var genericStateMenu = encounterGameMenuModel.GetGenericStateMenu();
        if (genericStateMenu != "army_wait")
        {
            args?.MenuContext?.GameMenu?.EndWait();
            if (string.IsNullOrEmpty(genericStateMenu))
            {
                GameMenu.ExitToLast();
            }
            else
            {
                GameMenu.SwitchToMenu(genericStateMenu);
            }

            return false;
        }

        return IsStableArmyWait(mainParty);
    }

    private static bool TryStartAttachedArmySiege()
    {
        if (Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId != "army_wait")
            return false;

        if (GetAttachedArmySiegeState(MobileParty.MainParty, out var settlement) != AttachedArmySiegeState.Ready)
            return false;

        StartAttachedArmySiege(settlement);
        return true;
    }

    private static void StartAttachedArmySiege(Settlement settlement)
    {
        using (new AllowedThread())
        {
            PlayerSiege.StartPlayerSiege(BattleSideEnum.Attacker, isSimulation: false, settlement);
            PlayerSiege.StartSiegePreparation();
        }
    }

    internal static AttachedArmySiegeState GetAttachedArmySiegeState(
        MobileParty mainParty,
        out Settlement settlement)
    {
        settlement = null;
        if (mainParty == null)
            return AttachedArmySiegeState.Incomplete;

        var army = mainParty.Army;
        var leaderParty = army?.LeaderParty;
        if (army == null)
        {
            return mainParty.AttachedTo != null || mainParty.BesiegerCamp != null
                ? AttachedArmySiegeState.Incomplete
                : AttachedArmySiegeState.None;
        }

        if (leaderParty == null)
            return AttachedArmySiegeState.Incomplete;

        if (leaderParty == mainParty)
            return AttachedArmySiegeState.None;

        if (mainParty.AttachedTo != leaderParty || leaderParty.Army != army)
            return AttachedArmySiegeState.Incomplete;

        var targetSettlement = army.AiBehaviorObject as Settlement;
        var mainPartyCamp = mainParty.BesiegerCamp;
        var leaderCamp = leaderParty.BesiegerCamp;
        if (targetSettlement == null)
        {
            return mainPartyCamp != null || leaderCamp != null
                ? AttachedArmySiegeState.Incomplete
                : AttachedArmySiegeState.None;
        }

        var siegeEvent = targetSettlement.SiegeEvent;
        if (siegeEvent == null)
        {
            return mainPartyCamp != null || leaderCamp != null
                ? AttachedArmySiegeState.Incomplete
                : AttachedArmySiegeState.None;
        }

        var liveCamp = siegeEvent.BesiegerCamp;
        if (mainPartyCamp == null && leaderCamp == null)
            return AttachedArmySiegeState.None;

        if (liveCamp == null ||
            mainPartyCamp != liveCamp ||
            leaderCamp != liveCamp ||
            liveCamp.SiegeEvent != siegeEvent ||
            siegeEvent.BesiegedSettlement != targetSettlement ||
            leaderParty.BesiegedSettlement != targetSettlement)
        {
            return AttachedArmySiegeState.Incomplete;
        }

        settlement = targetSettlement;
        return AttachedArmySiegeState.Ready;
    }

    internal static bool IsStableArmyWait(MobileParty mainParty)
    {
        var army = mainParty?.Army;
        var leaderParty = army?.LeaderParty;
        if (leaderParty == null)
            return false;

        return leaderParty == mainParty ||
            (mainParty.AttachedTo == leaderParty && leaderParty.Army == army);
    }

    [HarmonyPatch(typeof(PlayerArmyWaitBehavior), nameof(PlayerArmyWaitBehavior.wait_menu_army_leave_on_consequence))]
    [HarmonyPrefix]
    private static bool WaitMenuLeavePrefix(PlayerArmyWaitBehavior __instance, MenuCallbackArgs args)
    {
        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.Finish(true);
        }
        else
        {
            GameMenu.ExitToLast();
        }
        if (Settlement.CurrentSettlement != null)
        {
            MessageBroker.Instance.Publish(MobileParty.MainParty, new EndSettlementEncounterAttempted(MobileParty.MainParty));
            PartyBase.MainParty.SetVisualAsDirty();
        }
        var message = new MobilePartyInArmyRemoved(MobileParty.MainParty.Army, MobileParty.MainParty, MobileParty.MainParty);
        MessageBroker.Instance.Publish(__instance, message);
        return false;
    }
    [HarmonyPatch(typeof(PlayerArmyWaitBehavior), nameof(PlayerArmyWaitBehavior.wait_menu_army_abandon_on_consequence))]
    [HarmonyPrefix]
    private static bool Prefixwait_menu_army_abandon_on_consequence(PlayerArmyWaitBehavior __instance, MenuCallbackArgs args)
    {
        MessageBroker.Instance.Publish(__instance, new ChangeClanInfluence(Clan.PlayerClan, (int)(float)Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfAbandoningArmy()));
        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.Finish(true);
        }
        else
        {
            GameMenu.ExitToLast();
        }
        var message = new MobilePartyInArmyRemoved(MobileParty.MainParty.Army, MobileParty.MainParty, MobileParty.MainParty);
        ArmyPatches.RemoveMobilePartyInArmy(MobileParty.MainParty, MobileParty.MainParty.Army, MobileParty.MainParty);
        MessageBroker.Instance.Publish(__instance, message);
        return false;
    }
}
