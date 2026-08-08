using Common;
using Common.Messaging;
using GameInterface.Services.Hideouts.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Hideouts.Patches.Disable;

[HarmonyPatch(typeof(HideoutCampaignBehavior))]
internal class HideoutCampaignBehaviorPatch
{
    [HarmonyPatch(nameof(HideoutCampaignBehavior.ArrangeHideoutTroopCountsForMission))]
    [HarmonyPrefix]
    private static bool ArrangeHideoutTroopCountsForMission()
    {
        return ModInformation.IsServer;
    }

    [HarmonyPatch("OnTroopRosterManageDone")]
    [HarmonyPrefix]
    private static void OnTroopRosterManageDone()
    {
        PublishConsequence(HideoutCampaignConsequence.PrepareMission);
    }

    [HarmonyPatch("hideout_send_troops_result_failure_on_init")]
    [HarmonyPrefix]
    private static void HideoutSendTroopsResultFailureOnInit()
    {
        PublishConsequence(HideoutCampaignConsequence.SetAttackCooldown);
    }

    [HarmonyPatch("game_menu_hideout_place_on_init")]
    [HarmonyPrefix]
    private static void GameMenuHideoutPlaceOnInit()
    {
        var encounter = PlayerEncounter.Current;
        if (encounter == null)
            return;

        var battle = PlayerEncounter.Battle;
        if (battle != null && battle.WinningSide == encounter.PlayerSide)
            PublishConsequence(HideoutCampaignConsequence.GrantClearRewards);
    }

    [HarmonyPatch("hideout_send_troops_result_success_consequence")]
    [HarmonyPrefix]
    private static void HideoutSendTroopsResultSuccessConsequence()
    {
        PublishConsequence(HideoutCampaignConsequence.GrantClearRewards);
    }

    [HarmonyPatch(nameof(HideoutCampaignBehavior.HourlyTickSettlement))]
    [HarmonyPrefix]
    public static bool HourlyTickSettlement(Settlement settlement)
    {
        if (!ModInformation.IsServer)
        {
            return false;
        }

        if (settlement.IsHideout && settlement.Hideout.IsInfested && !settlement.Hideout.IsSpotted)
        {
            float hideoutSpottingDistance = Campaign.Current.Models.MapVisibilityModel.GetHideoutSpottingDistance();

            if (ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) == false)
                return false;

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
                return false;

            foreach (var item in playerManager.Players)
            {
                if (objectManager.TryGetObject<MobileParty>(item.MobilePartyId, out var mobileParty) && mobileParty.IsActive)
                {
                    float num = mobileParty.Position.DistanceSquared(settlement.Position);
                    float num2 = 1f - num / (hideoutSpottingDistance * hideoutSpottingDistance);
                    if (num2 > 0f && settlement.Parties.Count > 0 && MBRandom.RandomFloat < num2 && !settlement.Hideout.IsSpotted)
                    {
                        settlement.Hideout.IsSpotted = true;
                        settlement.IsVisible = true;
                        CampaignEventDispatcher.Instance.OnHideoutSpotted(mobileParty.Party, settlement.Party);
                        break;
                    }
                }
            }
        }
        return false;
    }

    private static void PublishConsequence(HideoutCampaignConsequence consequence, Settlement settlement = null)
    {
        if (!ModInformation.IsClient)
            return;

        settlement ??= Settlement.CurrentSettlement;
        if (settlement?.IsHideout != true)
            return;

        MessageBroker.Instance.Publish(
            settlement,
            new HideoutCampaignConsequenceRequested(settlement, consequence));
    }
}
