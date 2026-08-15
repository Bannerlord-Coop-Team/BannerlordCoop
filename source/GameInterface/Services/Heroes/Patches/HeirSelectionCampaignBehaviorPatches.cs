using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.Players.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(HeirSelectionCampaignBehavior))]
internal class HeirSelectionCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(HeirSelectionCampaignBehavior.OnBeforeMainCharacterDied))]
    public static bool OnBeforeMainCharacterDiedPrefix(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true)
    {
        if (!victim.IsPlayerHero()) return false;

        Dictionary<Hero, int> heirApparents = victim.Clan.GetHeirApparents();
        victim.AddDeathMark(killer, detail);

        // Delete player character and send back to character creation screen
        MessageBroker.Instance.Publish(null, new PlayerDeleteRequested());
        // Run a modified ShowGameStatistics on client?

        // TODO: Heir selection on player death
        //if (heirApparents.Count == 0)
        //{
            // Move existing above deletion into here for when there are no available heirs
        //}
        //else
        //{
        //    if (victim.IsPrisoner)
        //    {
        //        EndCaptivityAction.ApplyByDeath(victim);
        //    }
        //    if (PlayerEncounter.Current != null && (PlayerEncounter.Battle == null || !PlayerEncounter.Battle.IsFinalized))
        //    {
        //        PlayerEncounter.Finish(true);
        //    }
        //    CampaignEventDispatcher.Instance.OnHeirSelectionRequested(heirApparents);
        //}
        if (Campaign.Current.CurrentMenuContext != null)
        {
            GameMenu.ExitToLast();
        }

        return false;
    }
}
