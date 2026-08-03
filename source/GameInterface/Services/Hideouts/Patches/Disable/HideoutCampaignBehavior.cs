using Common;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Hideouts.Patches.Disable;

[HarmonyPatch(typeof(HideoutCampaignBehavior))]
internal class HideoutCampaignBehaviorPatch
{
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
                if (objectManager.TryGetObject<MobileParty>(item.MobilePartyId, out var mobileParty))
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
}
