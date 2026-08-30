using Common;
using Common.Messaging;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.UI.Cutscenes.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System.Collections.Generic;
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

        victim.AddDeathMark(killer, detail);

        MessageBroker.Instance.Publish(__instance, new InitiateCutscenePlayerCharacterDied(victim, killer, detail));

        Dictionary<Hero, int> heirApparents = victim.Clan.GetHeirApparents();
        if (heirApparents.Count == 0) // No heirs, client should be sent to game over screen
        {
            MessageBroker.Instance.Publish(__instance, new ClientGameOver(victim, killer, detail));
        }
        else // Client needs to select heirs and publish new character to control
        {
            if (victim.IsPrisoner)
            {
                EndCaptivityAction.ApplyByDeath(victim);
            }

            MessageBroker.Instance.Publish(__instance, new ClientSelectHeir(victim, heirApparents));
        }

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
