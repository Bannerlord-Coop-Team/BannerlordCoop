using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.UI.Cutscenes.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.HeirSelection.Patches;

[HarmonyPatch(typeof(HeirSelectionCampaignBehavior))]
internal class HeirSelectionCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(HeirSelectionCampaignBehavior.OnBeforeMainCharacterDied))]
    [HarmonyPrefix]
    public static bool OnBeforeMainCharacterDiedPrefix(HeirSelectionCampaignBehavior __instance, Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true)
    {
        if (ModInformation.IsClient || !victim.IsPlayerHero()) return false;

        // Rest of vanilla implementation is run from HeirSelectionHandler.Handle_PlayerHeirSelectionRequested.
        // Re-connecting clients need to process heir selection too so only broadcast cutscene message.
        MessageBroker.Instance.Publish(__instance, new InitiateCutscenePlayerCharacterDied(victim, killer, detail));

        return false;
    }

    [HarmonyPatch(nameof(HeirSelectionCampaignBehavior.OnBeforePlayerCharacterChanged))]
    [HarmonyPrefix]
    public static bool OnBeforePlayerCharacterChangedPrefix(Hero oldPlayer, Hero newPlayer)
    {
        // TODO: Implement for coop
        return false;
    }

    [HarmonyPatch(nameof(HeirSelectionCampaignBehavior.OnPlayerCharacterChanged))]
    [HarmonyPrefix]
    public static bool OnPlayerCharacterChangedPrefix(Hero oldPlayer, Hero newPlayer, MobileParty newMainParty, bool isMainPartyChanged)
    {
        // TODO: Implement for coop
        return false;
    }

    [HarmonyPatch(nameof(HeirSelectionCampaignBehavior.OnHeirSelectionOver))]
    [HarmonyPrefix]
    public static bool OnHeirSelectionOverPrefix(HeirSelectionCampaignBehavior __instance, Hero selectedHeir)
    {
        if (ModInformation.IsServer) return false;

        var message = new HeirSelectionOver(Hero.MainHero, selectedHeir);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}
