using Common;
using GameInterface.Configuration;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(DesertersCampaignBehavior))]
internal class DisableDesertersCampaignBehavior
{
    [HarmonyPatch(nameof(DesertersCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(DesertersCampaignBehavior))]
internal class DesertersCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(DesertersCampaignBehavior.SpawnDesertersParty))]
    [HarmonyPrefix]
    public static bool SpawnDesertersPartyPrefix(DesertersCampaignBehavior __instance, MapEvent mapEvent, TroopRoster troops, Settlement settlement)
    {
        CampaignVec2 deserterSpawnPosition = __instance.GetDeserterSpawnPosition(settlement);
        MobileParty mobileParty = BanditPartyComponent.CreateLooterParty(__instance.DeserterClan.StringId + "_1", __instance.DeserterClan, settlement, false, null, deserterSpawnPosition);

        // Use default value if negative
        var multiplier = 1f;
        if (ModConfigProvider.ModOptions.LooterPartySizeMultiplier >= 0)
            multiplier = ModConfigProvider.ModOptions.LooterPartySizeMultiplier;

        // Scale new deserter party roster based on config
        foreach (var troopRosterElement in troops.GetTroopRoster())
        {
            var newCharacterCount = (int)(troopRosterElement.Number * multiplier);
            var numberToAdd = newCharacterCount - troopRosterElement.Number;

            CharacterObject character = troopRosterElement.Character;
            troops.AddToCounts(character, numberToAdd, false, 0, 0, true, -1);

            // Avoid turning deserter parties to zero parties, keep a minimum
            if (troops.TotalManCount <= 0)
            {
                troops.AddToCounts(character, 1, false, 0, 0, true, -1);
            }
        }

        mobileParty.MemberRoster.Add(troops);
        __instance.InitializeDeserterParty(mobileParty);
        mobileParty.SetMovePatrolAroundPoint(mobileParty.Position, MobileParty.NavigationType.Default);
        PartyBaseHelper.SortRoster(mobileParty);

        return false;
    }

    [HarmonyPatch(nameof(DesertersCampaignBehavior.GetDeserterSpawnPosition))]
    [HarmonyPrefix]
    public static bool GetDeserterSpawnPositionPrefix(DesertersCampaignBehavior __instance, ref CampaignVec2 __result, Settlement settlement)
    {
        // Calculate without using MobileParty.MainParty
        CampaignVec2 campaignVec = NavigationHelper.FindPointAroundPosition(settlement.GatePosition, MobileParty.NavigationType.Default, __instance.DesertersSpawnRadiusAroundVillages, 0f, true, false);
        for (int i = 0; i < 15; i++)
        {
            CampaignVec2 campaignVec2 = NavigationHelper.FindReachablePointAroundPosition(campaignVec, MobileParty.NavigationType.Default, __instance.DesertersSpawnRadiusAroundVillages, 0f, false);
            if (NavigationHelper.IsPositionValidForNavigationType(campaignVec2, MobileParty.NavigationType.Default))
            {
                campaignVec = campaignVec2;
            }
        }
        __result = campaignVec;
        return false;
    }
}
