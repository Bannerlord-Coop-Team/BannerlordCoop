using Common;
using Common.Messaging;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(HeirSelectionCampaignBehavior))]
internal class HeirSelectionCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(HeirSelectionCampaignBehavior.OnBeforeMainCharacterDied))]
    [HarmonyPrefix]
    public static bool OnBeforeMainCharacterDiedPrefix(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true)
    {
        if (ModInformation.IsClient || !victim.IsPlayerHero()) return false;

        victim.AddDeathMark(killer, detail);

        Dictionary<Hero, int> heirApparents = victim.Clan.GetHeirApparents();
        if (heirApparents.Count == 0)
        {
            MessageBroker.Instance.Publish(null, new ClientGameOver(victim, killer, detail));
        }
        else
        {
            MessageBroker.Instance.Publish(null, new ClientGameOver(victim, killer, detail));

            ////TODO: Heir selection on player death instead of GameOver
            //if (victim.IsPrisoner)
            //{
            //    EndCaptivityAction.ApplyByDeath(victim);
            //}
            //if (PlayerEncounter.Current != null && (PlayerEncounter.Battle == null || !PlayerEncounter.Battle.IsFinalized))
            //{
            //    PlayerEncounter.Finish(true);
            //}
            //CampaignEventDispatcher.Instance.OnHeirSelectionRequested(heirApparents);
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
    public static bool OnHeirSelectionOverPrefix(Hero selectedHeir)
    {
        // TODO: Implement for coop
        return false;
    }
}
