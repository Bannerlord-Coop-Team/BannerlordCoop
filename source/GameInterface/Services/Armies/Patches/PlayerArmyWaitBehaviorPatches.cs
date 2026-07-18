using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies.Patches;

/// <summary>
/// Patch that sends the removal of the party to the server.
/// </summary>
[HarmonyPatch]
internal class PlayerArmyWaitBehaviorPatches
{
    [HarmonyPatch(typeof(PlayerArmyWaitBehavior), nameof(PlayerArmyWaitBehavior.OnTick))]
    static bool Prefix() => ModInformation.IsClient;
    private static readonly ILogger Logger = LogManager.GetLogger<PlayerArmyWaitBehaviorPatches>();
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
        MessageBroker.Instance.Publish(__instance, new ChangeClanInfluence(Clan.PlayerClan, (int)(-(float)Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfAbandoningArmy())));
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