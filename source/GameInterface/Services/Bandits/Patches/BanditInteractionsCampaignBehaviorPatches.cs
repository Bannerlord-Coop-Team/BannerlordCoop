using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Bandits.Messages;
using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Bandits.Patches;

[HarmonyPatch(typeof(BanditInteractionsCampaignBehavior))]
internal class DisableBanditInteractionsCampaignBehavior
{
    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
    {
        AccessTools.Method(typeof(BanditInteractionsCampaignBehavior), nameof(BanditInteractionsCampaignBehavior.OnPartyDestroyed)),
        //AccessTools.Method(typeof(BanditInteractionsCampaignBehavior), nameof(BanditInteractionsCampaignBehavior.OnSessionLaunched)), // Needed on client to load dialogue
    };

    static bool Prefix()
    {
        return ModInformation.IsServer;
    }
}

[HarmonyPatch(typeof(BanditInteractionsCampaignBehavior))]
internal class BanditInteractionsCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(BanditInteractionsCampaignBehavior.bandit_barter_successful_on_consequence))]
    [HarmonyPostfix]
    public static void BanditBarterSuccessfulOnConsequencePostfix(BanditInteractionsCampaignBehavior __instance)
    {
        // Locally update PlayerInteraction and send updated in postfix to save in CoopSession
        var message = new SetPlayerBanditInteraction(Hero.MainHero, MobileParty.ConversationParty, BanditInteractionsCampaignBehavior.PlayerInteraction.PaidOffParty);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(BanditInteractionsCampaignBehavior.bandit_neutral_greet_on_consequence))]
    [HarmonyPostfix]
    public static void BanditNeutralGreetOnConsequence(BanditInteractionsCampaignBehavior __instance)
    {
        // Locally update PlayerInteraction and send updated in postfix to save in CoopSession
        var message = new SetPlayerBanditInteraction(Hero.MainHero, MobileParty.ConversationParty, __instance.GetPlayerInteraction(MobileParty.ConversationParty));
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(BanditInteractionsCampaignBehavior.conversation_bandit_set_hostile_on_consequence))]
    [HarmonyPostfix]
    public static void ConversationBanditSetHostileOnConsequence(BanditInteractionsCampaignBehavior __instance)
    {
        // Locally update PlayerInteraction and send updated in postfix to save in CoopSession
        var message = new SetPlayerBanditInteraction(Hero.MainHero, MobileParty.ConversationParty, BanditInteractionsCampaignBehavior.PlayerInteraction.Hostile);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(BanditInteractionsCampaignBehavior.DoneButtonCondition))]
    [HarmonyPrefix]
    public static bool DoneButtonConditionPrefix(BanditInteractionsCampaignBehavior __instance, ref Tuple<bool, TextObject> __result, TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, int leftLimitNum, int rightLimitNum)
    {
        // This condition updates hero object states. Need to apply this on the server
        var message = new BanditPartyScreenDoneCondition(rightMemberRoster);
        MessageBroker.Instance.Publish(__instance, message);

        __result = new Tuple<bool, TextObject>(true, null);
        return false;
    }

    [HarmonyPatch(nameof(BanditInteractionsCampaignBehavior.GetMemberAndPrisonerRostersFromParties))]
    [HarmonyPrefix]
    public static bool GetMemberAndPrisonerRostersFromPartiesPrefix(BanditInteractionsCampaignBehavior __instance, List<MobileParty> parties, ref TroopRoster troopsTakenAsMember, ref TroopRoster troopsTakenAsPrisoner, bool doBanditsJoinPlayerSide)
    {
        // Re-implement the parts of this method safe to run on clients.
        // Even in TaleWorlds' code doBanditsJoinPlayerSide is always true so troopsTakenAsPrisoner is never used.
        // This patch keeps this behavior to remain similar to the original in case of changes in future versions.
        foreach (MobileParty mobileParty in parties)
        {
            for (int i = 0; i < mobileParty.MemberRoster.Count; i++)
            {
                if (!mobileParty.MemberRoster.GetCharacterAtIndex(i).IsHero)
                {
                    if (doBanditsJoinPlayerSide)
                    {
                        troopsTakenAsMember.AddToCounts(mobileParty.MemberRoster.GetCharacterAtIndex(i), mobileParty.MemberRoster.GetElementNumber(i), false, 0, 0, true, -1);
                    }
                    else
                    {
                        troopsTakenAsPrisoner.AddToCounts(mobileParty.MemberRoster.GetCharacterAtIndex(i), mobileParty.MemberRoster.GetElementNumber(i), false, 0, 0, true, -1);
                    }
                }
            }
            for (int j = mobileParty.PrisonRoster.Count - 1; j > -1; j--)
            {
                CharacterObject characterAtIndex = mobileParty.PrisonRoster.GetCharacterAtIndex(j);
                if (!characterAtIndex.IsHero)
                {
                    troopsTakenAsMember.AddToCounts(mobileParty.PrisonRoster.GetCharacterAtIndex(j), mobileParty.PrisonRoster.GetElementNumber(j), false, 0, 0, true, -1);
                }
            }
        }

        // Run rest of this method on the server to sync actions properly
        var message = new GetBanditMemberAndPrisonerRosters(Clan.PlayerClan, MobileParty.MainParty, parties, doBanditsJoinPlayerSide);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    [HarmonyPatch(nameof(BanditInteractionsCampaignBehavior.OpenRosterScreenAfterBanditEncounter))]
    [HarmonyPrefix]
    public static bool OpenRosterScreenAfterBanditEncounterPrefix(BanditInteractionsCampaignBehavior __instance, MobileParty conversationParty, bool doBanditsJoinPlayerSide)
    {
        // Run enemy surrender logic on the server to properly start and end a map event
        // Opening and closing a map event with involved looters is needed to apply 
        // certain affects like increasing security around settlements.
        if (!doBanditsJoinPlayerSide)
        {
            if (PlayerEncounter.Battle == null)
            {
                PlayerEncounter.StartBattle();
            }

            PlayerEncounter.Battle.SetOverrideWinner(PlayerEncounter.Battle.PlayerSide);
            PlayerEncounter.EnemySurrender = true;

            // Nullify player's map event to use patched PlayerEncounter update.
            // Without this, client gets a duplicate loot screen cycle.
            PlayerEncounter.Current._mapEvent = null;

            return false;
        }

        // Instead of the vanilla check to also include all nearby enemy parties in the calculation,
        // only add the enemy party in the conversation to the result.
        // Vanilla assumes a paused map outside of the conversation and allowing nearby bandit parties to
        // disappear after dialogue in a live session would be very strange behavior that could cause issues.
        List<MobileParty> enemySideParties = new List<MobileParty>();
        enemySideParties.Add(PlayerEncounter.EncounteredMobileParty);

        using (new AllowedThread())
        {
            TroopRoster troopsTakenAsMember = TroopRoster.CreateDummyTroopRoster();
            TroopRoster troopsTakenAsPrisoner = TroopRoster.CreateDummyTroopRoster(); // This troopRoster is not used but is still populated in TaleWorlds code

            __instance.GetMemberAndPrisonerRostersFromParties(enemySideParties, ref troopsTakenAsMember, ref troopsTakenAsPrisoner, doBanditsJoinPlayerSide);
            PartyScreenHelper.OpenScreenWithCondition(new IsTroopTransferableDelegate(__instance.IsTroopTransferable), new PartyPresentationDoneButtonConditionDelegate(__instance.DoneButtonCondition), new PartyPresentationDoneButtonDelegate(__instance.OnDoneClicked), null, PartyScreenLogic.TransferState.Transferable, PartyScreenLogic.TransferState.Transferable, PlayerEncounter.EncounteredParty.Name, troopsTakenAsMember.TotalManCount, false, false, PartyScreenHelper.PartyScreenMode.TroopsManage, troopsTakenAsMember, null);
            MBList<Ship> mblist = conversationParty.Ships.ToMBList<Ship>();
            if (!mblist.IsEmpty<Ship>())
            {
                PortStateHelper.OpenAsLoot(mblist, null);
            }
        }

        var message = new RosterScreenAfterBanditEncounter(enemySideParties, MobileParty.MainParty);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}