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
        private static readonly ILogger Logger = LogManager.GetLogger<UnitRecruitPatch>();

        [HarmonyPrefix]
        [HarmonyPatch("AddNewElement")]
        private static bool PrefixAddNewElement(TroopRoster __instance, CharacterObject character, bool insertAtFront = false, int insertionIndex = -1)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                return true;
            }

            PartyBase ownerParty = __instance.OwnerParty;

            if (ownerParty == null)
            {
                Logger.Error("OwnerParty was null for troop roster");
                return false;
            }

            var message = new NewTroopAdded(
                character.StringId,
                ownerParty.MobileParty.StringId,
                __instance.IsPrisonRoster,
                insertAtFront,
                insertionIndex);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TroopRoster.AddToCountsAtIndex))]
        private static bool PrefixAddToCountsAtIndex(TroopRoster __instance, int index, int countChange, int woundedCountChange = 0, int xpChange = 0, bool removeDepleted = true)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client added unmanaged item: {callstack}", Environment.StackTrace);
                return true;
            }

            PartyBase ownerParty = __instance.OwnerParty;

            if (ownerParty == null)
            {
                Logger.Error("OwnerParty was null for troop roster");
                return false;
            }

            var message = new TroopIndexAdded(
                ownerParty.MobileParty.StringId,
                __instance.IsPrisonRoster,
                index,
                countChange,
                woundedCountChange,
                xpChange,
                removeDepleted);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }

        public static void RunOriginalAddNewElement(CharacterObject character, MobileParty party, bool isPrisonerRoster, bool insertAtFront = false, int insertionIndex = -1)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    TroopRoster roster = isPrisonerRoster ? party.PrisonRoster : party.MemberRoster;

                    roster.AddNewElement(character, insertAtFront, insertionIndex);
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
                    TroopRoster roster = isPrisonerRoster ? party.PrisonRoster : party.MemberRoster;

                    roster.RemoveZeroCounts();

                    if(roster.Count > index)
                    {
                        roster.AddToCountsAtIndex(index, countChange, woundedCountChange, xpChange, removeDepleted);
                    }
                }
            }, true);
        }
    }
}
