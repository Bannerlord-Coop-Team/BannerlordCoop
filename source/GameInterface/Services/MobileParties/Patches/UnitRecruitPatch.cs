using Common;
using Common.Extensions;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using Serilog;
using Serilog.Core;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(TroopRoster))]
    public class UnitRecruitPatch
    {
        private static PropertyInfo TroopRoster_OwnerParty => typeof(TroopRoster).GetProperty("OwnerParty", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo TroopRoster_AddNewElement = typeof(TroopRoster).GetMethod("AddNewElement", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly ILogger Logger = LogManager.GetLogger<UnitRecruitPatch>();

        [HarmonyPrefix]
        [HarmonyPatch("AddNewElement")]
        private static bool PrefixAddNewElement(TroopRoster __instance, CharacterObject character, bool insertAtFront = false, int insertionIndex = -1)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (PolicyProvider.AllowOriginalCalls) return true;

            PartyBase ownerParty = (PartyBase)TroopRoster_OwnerParty.GetValue(__instance);

            if (ownerParty == null) return false;

            MessageBroker.Instance.Publish(__instance, new NewTroopAdded(
                character.StringId, 
                ownerParty.MobileParty.StringId, 
                __instance.IsPrisonRoster, 
                insertAtFront, 
                insertionIndex));

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TroopRoster.AddToCountsAtIndex))]
        private static bool PrefixAddToCountsAtIndex(TroopRoster __instance, int index, int countChange, int woundedCountChange = 0, int xpChange = 0, bool removeDepleted = true)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (PolicyProvider.AllowOriginalCalls) return true;

            PartyBase ownerParty = (PartyBase)TroopRoster_OwnerParty.GetValue(__instance);

            if (ownerParty == null) return false;

            MessageBroker.Instance.Publish(__instance, new TroopIndexAdded(
                ownerParty.MobileParty.StringId, 
                __instance.IsPrisonRoster,
                index,
                countChange,
                woundedCountChange,
                xpChange,
                removeDepleted));

            return false;
        }

        public static void RunOriginalAddNewElement(CharacterObject character, MobileParty party, bool isPrisonerRoster, bool insertAtFront = false, int insertionIndex = -1)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    if (isPrisonerRoster)
                    {
                        TroopRoster_AddNewElement.Invoke(party.PrisonRoster, new object[] { character, insertAtFront, insertionIndex });
                    }
                    else
                    {
                        TroopRoster_AddNewElement.Invoke(party.MemberRoster, new object[] { character, insertAtFront, insertionIndex });
                    }
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
