using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(TroopRoster))]
    public class UnitRecruitPatch
    {
        private static PropertyInfo TroopRoster_OwnerParty => typeof(TroopRoster).GetProperty("OwnerParty", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly AllowedInstance<CharacterObject> AllowedInstance = new AllowedInstance<CharacterObject>();

        [HarmonyPatch(nameof(TroopRoster.AddToCounts))]
        public static bool Prefix(TroopRoster __instance, CharacterObject character, int count, bool insertAtFront = false, int woundedCount = 0, int xpChange = 0, bool removeDepleted = true, int index = -1)
        {
            if (AllowedInstance.IsAllowed(character)) return true;

            if (PolicyProvider.AllowOriginalCalls) return true;

            PartyBase ownerParty = (PartyBase)TroopRoster_OwnerParty.GetValue(__instance);

            if (ownerParty == null) return false;

            if (ownerParty.MobileParty.IsPartyControlled() == false) return false;

            MessageBroker.Instance.Publish(__instance, new TroopCountChanged(character.StringId, count, ownerParty.MobileParty.StringId, __instance.IsPrisonRoster));

            return false;
        }


        public static void RunOriginalAddToCounts(CharacterObject character, int amount, MobileParty party, bool isPrisonerRoster)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = character;

                if (isPrisonerRoster)
                {
                    party.PrisonRoster.AddToCounts(character, amount);
                }
                else
                {
                    party.MemberRoster.AddToCounts(character, amount);
                }
            }
        }
    }
}
