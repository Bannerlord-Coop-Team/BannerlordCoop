using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Traits.Patches;

[HarmonyPatch(typeof(ChangeVillageStateAction))]
internal class ChangeVillageStateActionPatches
{
    // Replaces PlayerVariablesBehavior.OnVillageLooted, which checks the PlayerEncounter that only clients have
    [HarmonyPatch(nameof(ChangeVillageStateAction.ApplyBySettingToLooted))]
    [HarmonyPostfix]
    public static void ApplyBySettingToLootedPostfix(MobileParty raider)
    {
        if (ModInformation.IsClient) return;

        var mapEvent = raider?.MapEvent;
        if (mapEvent == null) return;

        foreach (var mapEventParty in mapEvent.AttackerSide.Parties.ToList())
        {
            var mobileParty = mapEventParty.Party.MobileParty;
            if (mobileParty == null || !mobileParty.IsPlayerParty()) continue;

            using (new MainHeroSubstitutionScope(mobileParty.LeaderHero, mobileParty))
            {
                TraitLevelingHelper.OnVillageRaided();
            }
        }
    }
}
