using Common.Messaging;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.Heroes.Extensions;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(HeirSelectionCampaignBehavior))]
internal class HeirSelectionCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(HeirSelectionCampaignBehavior.OnBeforeMainCharacterDied))]
    [HarmonyPrefix]
    public static bool OnBeforeMainCharacterDiedPrefix(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true)
    {
        if (!victim.IsPlayerHero()) return false;

        Dictionary<Hero, int> heirApparents = victim.Clan.GetHeirApparents();
        victim.AddDeathMark(killer, detail);

        // Delete player character and send back to character creation screen
        Dictionary<TroopRosterElement, int> dictionary = new();

        if (victim.PartyBelongedTo?.Party?.MemberRoster != null)
        {
            foreach (TroopRosterElement troopRosterElement in victim.PartyBelongedTo.Party.MemberRoster.GetTroopRoster())
            {
                if (!troopRosterElement.Character.IsHero || !troopRosterElement.Character.HeroObject.IsPlayerHero())
                {
                    dictionary.Add(troopRosterElement, troopRosterElement.Number);
                }
            }
            foreach (KeyValuePair<TroopRosterElement, int> keyValuePair in dictionary)
            {
                victim.PartyBelongedTo.Party.MemberRoster.RemoveTroop(keyValuePair.Key.Character, keyValuePair.Value, default, 0);
            }
        }

        // Re-implementation of GameOverCleanup() for coop
        GiveGoldAction.ApplyBetweenCharacters(victim, null, victim.Gold, true);
        if (victim.PartyBelongedTo != null)
        {
            var playerParty = victim.PartyBelongedTo;

            playerParty.Party.ItemRoster.Clear();
            playerParty.Party.MemberRoster.Clear();
            playerParty.Party.PrisonRoster.Clear();
            playerParty.IsVisible = false;
            playerParty.IsActive = false;
            playerParty.Party.SetVisualAsDirty();
        }
        if (victim.MapFaction.IsKingdomFaction && victim.Clan?.Kingdom?.Leader.IsPlayerHero() != false)
        {
            DestroyKingdomAction.ApplyByKingdomLeaderDeath(victim.Clan.Kingdom);
        }

        MessageBroker.Instance.Publish(null, new ClientGameOver(victim, detail));

        // TODO: Heir selection on player death
        //if (heirApparents.Count == 0)
        //{
        // Move existing above deletion logic into here for when there are no available heirs
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
