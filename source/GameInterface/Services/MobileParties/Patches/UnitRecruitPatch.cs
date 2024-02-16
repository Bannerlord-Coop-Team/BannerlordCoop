using Common;
using Common.Extensions;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(TroopRoster))]
    public class UnitRecruitPatch
    {
        static readonly Func<TroopRoster, PartyBase> TroopRoster_OwnerParty = 
            typeof(TroopRoster)
        .GetProperty("OwnerParty", BindingFlags.Instance | BindingFlags.NonPublic)
        .BuildUntypedGetter<TroopRoster, PartyBase>();

        static readonly Func<TroopRoster, CharacterObject, bool, int, int> TroopRoster_AddNewElement = 
            typeof(TroopRoster)
            .GetMethod("AddNewElement", BindingFlags.Instance | BindingFlags.NonPublic)
            .BuildDelegate<Func<TroopRoster, CharacterObject, bool, int, int>>();

        private static readonly ILogger Logger = LogManager.GetLogger<UnitRecruitPatch>();

        [HarmonyPrefix]
        [HarmonyPatch("AddNewElement")]
        private static bool PrefixAddNewElement(TroopRoster __instance, CharacterObject character, bool insertAtFront = false, int insertionIndex = -1)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            PartyBase ownerParty = TroopRoster_OwnerParty(__instance);

            if (ownerParty == null) return false;

            MessageBroker.Instance.Publish(__instance, new NewTroopAdded(
                character.StringId, 
                ownerParty.MobileParty.StringId, 
                __instance.IsPrisonRoster, 
                insertAtFront, 
                insertionIndex));

            return ModInformation.IsServer;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TroopRoster.AddToCountsAtIndex))]
        private static bool PrefixAddToCountsAtIndex(TroopRoster __instance, int index, int countChange, int woundedCountChange = 0, int xpChange = 0, bool removeDepleted = true)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            PartyBase ownerParty = TroopRoster_OwnerParty(__instance);

            if (ownerParty == null) return false;

            MessageBroker.Instance.Publish(__instance, new TroopIndexAdded(
                ownerParty.MobileParty.StringId, 
                __instance.IsPrisonRoster,
                index,
                countChange,
                woundedCountChange,
                xpChange,
                removeDepleted));

            return ModInformation.IsServer;
        }

        public static void RunOriginalAddNewElement(CharacterObject character, MobileParty party, bool isPrisonerRoster, bool insertAtFront = false, int insertionIndex = -1)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    TroopRoster roster;
                    if (isPrisonerRoster)
                    {
                        roster = party.PrisonRoster;
                    }
                    else
                    {
                        roster = party.MemberRoster;
                    }
                    TroopRoster_AddNewElement(roster, character, insertAtFront, insertionIndex);
                }
            }, true);
        }

        public static void RunOriginalAddToCountsAtIndex(MobileParty party, bool isPrisonerRoster, int index, int countChange, int woundedCountChange, int xpChange, bool removeDepleted)
        {
            //RemoveZeroCounts might need to be moved its own patch/handler for performance
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    if (isPrisonerRoster)
                    {
                        party.PrisonRoster.RemoveZeroCounts();
                        if (party.PrisonRoster.Count > index)
                        {
                            party.PrisonRoster.AddToCountsAtIndex(index, countChange, woundedCountChange, xpChange, removeDepleted);
                        }
                    }
                    else
                    {
                        party.MemberRoster.RemoveZeroCounts();
                        if(party.MemberRoster.Count > index)
                        {
                            party.MemberRoster.AddToCountsAtIndex(index, countChange, woundedCountChange, xpChange, removeDepleted);
                        }
                    }
                }
            }, true);
        }
    }
}
