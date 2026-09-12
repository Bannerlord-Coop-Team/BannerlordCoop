using Common;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Characters.Patches;

[HarmonyPatch(typeof(CharacterDevelopmentCampaignBehavior))]
internal class DisableCharacterDevelopmentCampaignBehavior
{
    [HarmonyPatch(nameof(CharacterDevelopmentCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

/// <summary>
/// OnCharacterCreationIsOver() shouldn't need to be patched. This event is only fired on clients after creating their character.
/// It develops character stats of non-player clan members, which would only be used by the campaign (not sandbox) when the player starts with family members.
/// </summary>
[HarmonyPatch(typeof(CharacterDevelopmentCampaignBehavior))]
internal class CharacterDevelopmentCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(CharacterDevelopmentCampaignBehavior.ShouldDevelopCharacterStats))]
    [HarmonyPrefix]
    public static bool ShouldDevelopCharacterStatsPrefix(ref bool __result, Hero hero)
    {
        if (!hero.IsChild && hero.IsAlive && (hero.Clan == null || !hero.Clan.IsPlayerClan() || (!hero.IsPlayerHero() && CampaignOptions.AutoAllocateClanMemberPerks)))
        {
            MobileParty partyBelongedTo = hero.PartyBelongedTo;
            __result = ((partyBelongedTo != null) ? partyBelongedTo.MapEvent : null) == null;
            return false;
        }

        __result = false;
        return false;
    }
}