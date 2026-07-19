using Common;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

/// <summary>Prevents the Party Screen from opening a local conversation with a captured player.</summary>
[HarmonyPatch(typeof(PartyVM))]
internal static class PlayerPartyPrisonerTalkPatches
{
    [HarmonyPatch(nameof(PartyVM.ExecuteTalk))]
    [HarmonyPrefix]
    private static bool ExecuteTalkPrefix(PartyVM __instance)
    {
        if (ModInformation.IsServer) return true;

        var currentCharacter = __instance?.CurrentCharacter;
        if (!ShouldBlockTalk(currentCharacter?.Character?.HeroObject, currentCharacter?.IsPrisoner == true))
            return true;

        ConversationPartyHold.ShowPlayerUnavailableMessage();
        return false;
    }

    internal static bool ShouldBlockTalk(Hero hero, bool isPrisoner) =>
        isPrisoner && hero?.IsPlayerHero() == true;
}
