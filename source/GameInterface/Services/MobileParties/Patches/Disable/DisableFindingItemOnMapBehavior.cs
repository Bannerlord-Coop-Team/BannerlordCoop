using Common;
using Common.Messaging;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(FindingItemOnMapBehavior))]
internal class DisableFindingItemOnMapBehavior
{
    [HarmonyPatch(nameof(FindingItemOnMapBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(FindingItemOnMapBehavior))]
internal class FindingItemOnMapBehaviorPatches
{
    [HarmonyPatch(nameof(FindingItemOnMapBehavior.DailyTickParty))]
    [HarmonyPrefix]
    public static bool DailyTickPartyPrefix(FindingItemOnMapBehavior __instance, MobileParty party)
    {
        if (MBRandom.RandomFloat < DefaultPerks.Scouting.BeastWhisperer.PrimaryBonus && party.HasPerk(DefaultPerks.Scouting.BeastWhisperer, false))
        {
            TerrainType faceTerrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(party.CurrentNavigationFace);
            if (faceTerrainType == TerrainType.Steppe || faceTerrainType == TerrainType.Plain)
            {
                ItemObject randomElementWithPredicate = Items.All.GetRandomElementWithPredicate((ItemObject x) => x.IsMountable && !x.NotMerchandise);
                if (randomElementWithPredicate != null)
                {
                    party.ItemRoster.AddToCounts(randomElementWithPredicate, 1);
                    if (party.IsPlayerParty())
                    {
                        MessageBroker.Instance.Publish(__instance, new NotifyFoundItemOnMap(party, 1, randomElementWithPredicate.Name));
                    }
                }
            }
        }

        return false;
    }
}