using Common;
using Common.Util;
using HarmonyLib;
using SandBox.GauntletUI;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace GameInterface.Services.Party.Patches;

[HarmonyPatch]
internal class GauntletPartyScreenPatches
{
    [ThreadStatic]
    private static CharacterObject conversationCharacter;

    [ThreadStatic]
    private static bool conversationCharacterIsPrisoner;

    [HarmonyPatch(typeof(PartyVM), nameof(PartyVM.ExecuteOpenConversation))]
    [HarmonyPrefix]
    public static void ExecuteOpenConversationPrefix(PartyVM __instance)
    {
        if (ModInformation.IsServer) return;

        var currentCharacter = __instance.CurrentCharacter;
        if (currentCharacter?.Character == null) return;

        conversationCharacter = currentCharacter.Character;
        conversationCharacterIsPrisoner = currentCharacter.IsPrisoner;
    }

    [HarmonyPatch(typeof(GauntletPartyScreen), nameof(GauntletPartyScreen.OnResume))]
    [HarmonyPrefix]
    public static void OnResumePrefix(GauntletPartyScreen __instance)
    {
        if (ModInformation.IsServer) return;
        if (__instance._dataSource?.IsInConversation != true) return;

        var character = conversationCharacter;
        bool isPrisoner = conversationCharacterIsPrisoner;
        conversationCharacter = null;
        conversationCharacterIsPrisoner = false;
        if (character == null) return;

        var partyScreenLogic = __instance._partyState.PartyScreenLogic;
        var partyRoster = isPrisoner
            ? partyScreenLogic.RightOwnerParty.PrisonRoster
            : partyScreenLogic.RightOwnerParty.MemberRoster;
        if (partyRoster.GetTroopCount(character) > 0) return;

        var initialRoster = isPrisoner
            ? partyScreenLogic._initialData.RightPrisonerRoster
            : partyScreenLogic._initialData.RightMemberRoster;
        var currentRoster = isPrisoner
            ? partyScreenLogic.CurrentData.RightPrisonerRoster
            : partyScreenLogic.CurrentData.RightMemberRoster;
        var savedRoster = partyScreenLogic._savedData == null
            ? null
            : isPrisoner
                ? partyScreenLogic._savedData.RightPrisonerRoster
                : partyScreenLogic._savedData.RightMemberRoster;

        using (new AllowedThread())
        {
            RemoveCharacter(initialRoster, character);
            RemoveCharacter(currentRoster, character);
            if (savedRoster != null) RemoveCharacter(savedRoster, character);
        }
    }

    private static void RemoveCharacter(TroopRoster roster, CharacterObject character)
    {
        int count = roster.GetTroopCount(character);
        if (count > 0) roster.RemoveTroop(character, count);
        roster.RemoveZeroCounts();
        roster.InitializeCachedData();
    }
}
